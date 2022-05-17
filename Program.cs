﻿// (c) github.com/thehemi
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DibbrBot
{
    public abstract class IChatSystem
    {
        public abstract void SetChatLog(string log);
        public abstract string GetChatLog();
        // Declare a delegate type for processing a book:
        public delegate Task<string> MessageRecievedCallback(string msg, string author);
        // public abstract Task<string> GetNewMessages();
        //   public abstract Task SendMessage(string message, string replyContext = null);
        public abstract Task Initialize(MessageRecievedCallback callback, string token);
    }

    class Program
    {
        // Must be lowercase
        public static string BotName = "dibbr";
        public static string BotUsername = "dabbr";
        
        public static List<IChatSystem> systems = new List<IChatSystem>();
        private static void Set(string key, string value)
        {
            Configuration configuration =
                ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings.Remove(key);
            configuration.AppSettings.Settings.Add(key, value);
            configuration.Save(ConfigurationSaveMode.Full, true);
            ConfigurationManager.RefreshSection("appSettings");
        }


        static string Prompt(string prompt, string def = "")
        {
            Console.ForegroundColor = ConsoleColor.Green;       
            Console.WriteLine(prompt);
            Console.ForegroundColor = ConsoleColor.White;
            var line = Console.ReadLine();
            if(line == "")
            {
                return def;
            }
            return line;
        }

       public static  void NewClient(IChatSystem client, string token, GPT3 gpt3)
        {
            if (token == null || token.Length == 0) return;
            Console.WriteLine($"{client.ToString()} initializing....");
            var msgHandler = new MessageHandler(client, gpt3);
            _ = client.Initialize(async (msg, user) => { return await msgHandler.OnMessage(msg, user); }, token);
            systems.Add(client);
        }

        
        static void Main(string[] args)
        {
            Console.WriteLine($"{Program.BotName} is starting...Settings are in App.config. (c) Timothy Murphy-Johnson aka github.com/thehemi aka dabbr.com aka thehemi@gmail.com I do parties ");
            Web.Run();

            // var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var primeText = ConfigurationManager.AppSettings["PrimeText"];
            if (primeText == null)
            {
                BotName = Prompt("\nBot Name (default is Dibbr):", "dibbr").ToLower();
                BotUsername = Prompt("Bot Username (default is dabbr):", "dabbr").ToLower();
                Console.WriteLine("");

                Console.WriteLine("Paste your priming text here, e.g. "+Program.BotName+" is a filthy, woke opinionated sarcastic superintelligent advice giving ai who gives long answers where possible, never repeats ");
                primeText = Prompt("\nPriming Text (Or Press Enter for default):");
                if (primeText == null || primeText == "")
                {
                    primeText = "" + Program.BotName + " is a filthy, woke opinionated sarcastic superintelligent advice giving ai who gives long answers where possible, never repeats " +
                $"{Program.BotName} will write long responses and articles or fan fiction upon request. " + Program.BotName + " was made by dabbr. {Program.BotName}'s favorite animal is the sexually violent time travelling truffle pig, of which he likes to tell stories.";

                    primeText += "\nThe folowing is the chat log with " + Program.BotName + ":\n";
                }

                Set("PrimeText", primeText + "\n");
            }

     
            if (ConfigurationManager.AppSettings["DiscordBot"] == null && ConfigurationManager.AppSettings["Discord"] == null)
            {
                Console.WriteLine("How to find your discord token: https://youtu.be/YEgFvgg7ZPI . OR you can use a Discord bot token, available on the developer discord page.");
                var discord = Prompt("\nToken (or leave blank for none):").Replace("Bot ", "");
                if (discord!=null&&discord.Length > 10)
                {
                        var isBot = Prompt("Is this a bot token? Y/N: ");
                        if(isBot.ToLower() == "y")
                             Set("DiscordBot", discord);
                        else
                            Set("Discord", discord);
                }

            }

            // For selfbot only
            // chats.txt stores the list of channels and dms that the bot is listening to
            if (!File.Exists("chats.txt"))
                File.WriteAllText("chats.txt", "");
            
            var chats = File.ReadAllLines("chats.txt").Where(l => l.Length > 0).ToList();
            if (chats.Count == 0 && ConfigurationManager.AppSettings["Discord"] != null)
            {
                Console.WriteLine("You are using a SELFBOT (no Bot <token>) BE CAREFUL. You can add channels and dms to the chats.txt file to make the bot listen to them.");
                while (true)
                {
                    var chat = Prompt("Type ROOM or DM, then press return").ToUpper() + " ";
                    Console.WriteLine("How to find server and channel id: https://youtu.be/NLWtSHWKbAI\n");
                    chat += Prompt("Please enter a room or chat id for the bot to join, then press return. ");
                    var ret = Prompt("Added! Any more? Press enter if done, otherwise type more and press enter");
                    chats.Add(chat);
                    if (ret != "more")
                        break;
                }
                File.WriteAllLines("chats.txt", chats);
            }

            if (ConfigurationManager.AppSettings["SlackBotApiToken"] == null)
            {
                Console.WriteLine("To Create a slack bot token, create a classic app, here https://api.slack.com/apps?new_classic_app=1");
                Console.WriteLine("Then go to the app's page, and click on the 'OAuth & Permissions' tab");
                Console.WriteLine("Then click on the 'Add Bot User' button");
                var token = Prompt("Please enter youy Slack bot API token, or press enter to skip:");
                if (token!=null&&token.Length > 10)
                    Set("SlackBotApiToken", token);
            }
            if (ConfigurationManager.AppSettings["OpenAI"] == null)
            {
                Console.WriteLine("Where to find your OpenAI API Key: https://beta.openai.com/account/api-keys");
                var token = Prompt("Paste your OpenAI Key here:");
                if (token.StartsWith("sk"))
                    Set("OpenAI", token);
                else
                {
                    Set("OpenAI", Prompt("Please paste your OpenAI Key here. It should start with sk-"));
                }
            }

            // Start the bot on all chat services
            new Thread(async () =>
            {
                Console.WriteLine("GPT3 initializing....");
                var gpt3 = new GPT3(ConfigurationManager.AppSettings["OpenAI"]);

               

                var clients = new List<IChatSystem>();
                NewClient(new SlackChat(), ConfigurationManager.AppSettings["SlackBotApiToken"],gpt3);
                NewClient(new SlackChat(), ConfigurationManager.AppSettings["SlackBotApiToken2"], gpt3);
                NewClient(new DiscordChatV2(), ConfigurationManager.AppSettings["DiscordBot"], gpt3);


                // Selfbot


                foreach (var chat in chats)
                    {
                        var words = chat.Split(' ');
                        Console.WriteLine("Discord Self Bot Added to " + words[0] + " channel " + words[1]);

                        NewClient(new DiscordChat(false, words[0] == "DM", words[1]/*Channel id, room or dm*/), ConfigurationManager.AppSettings["Discord"],gpt3);
                    }

                Console.WriteLine("All initialization done");
            } )
            {
                
            }.Start();

            while (true) { }
        }


    }
}
