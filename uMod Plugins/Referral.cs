using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("Referral", "birthdates", "0.1", ResourceId = 0)]
    public class Referral : RustPlugin
    {

        private new void LoadDefaultConfig()
        {
            PrintWarning("Loading the dafault config file! ../oxide/config/Referral");
            Config["GrantedPermission"] = "referral.use";
            
            
        }

        void Loaded()
        {
            permission.RegisterPermission("referral.use", this);
            if (!permission.PermissionExists(Config["GrantedPermission"].ToString()))
            {
                PrintWarning("ERROR: The permission you are trying to use for the referral command doesn't exist! So we have set it to referral.use");
                Config["GrantedPermission"] = "referral.use";
                SaveConfig();

            }

        }


        private new void LoadDefaultMessages()
        {
            PrintWarning("Loading the language file! ../oxide/lang/en");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermission", "You don't have permission."},
                {"ThankYouMessage", "Thank you for inviting {0} to the server! For this we have granted you a special permission."},
                {"SuccessMessage", "Thanks for joining the server! {0} will now get their reward" },
                {"SuccessMessage_2", "You have successfuly used the code {0}."},
                {"ValidArgs", "ERROR: Please specify a valid player/code to refer/use. Make sure they are online!" },
                {"ValidArgsConsole", "ERROR: Please specify valid arguments! Usage: refercode.add <code> <permission>"},
                {"AlreadyReferred","ERROR: You have already referred a user."},
                {"CodeAlreadyUsed", "ERROR: You have already used this code." },
                {"ValidCode", "ERROR: Please use a valid code." },
                {"PermDoesNotExist", "ERROR: That permission doesn't exist!"},
                {"LongerCode", "ERROR: Due to security reasons you need to have a code that has nine or more numbers."}

            }, this);
        }


        [ConsoleCommand("refercodes")]
        void codeList(ConsoleSystem.Arg arg)
        {
            if (Config["codes"] != null)
            {
                arg.ReplyWith(Config["codes"].ToString());
            } else
            {
                arg.ReplyWith("There are no codes yet.");
            }
        }


        [ConsoleCommand("refercode.add")]
        void addReferralCode(ConsoleSystem.Arg arg)
        {
            if (arg.HasArgs(2))
            {
                long code = 0;
                if(arg.GetString(0) == "random")
                {
                    System.Random r = new System.Random();


                    int num1 = r.Next(1,11);
                    int num2 = r.Next(1, 11);
                    int num3 = r.Next(1, 11);
                    int num4 = r.Next(1, 11);
                    int num5 = r.Next(1, 11);
                    int num6 = r.Next(1, 11);
                    int num7 = r.Next(1, 11);
                    int num8 = r.Next(1, 11);
                    int num9 = r.Next(1, 11);
                    code = Convert.ToInt64(num1.ToString() + num2.ToString() + num3.ToString() + num4.ToString() + num5.ToString() + num6.ToString() + num7.ToString() + num8.ToString() + num9.ToString());
                } else
                {
                    code = arg.GetInt(0);
                }
                 
                string cPermission = arg.GetString(1);
                if (Config[code.ToString()] != null)
                {
                    arg.ReplyWith("ERROR: The code already exists.");
                }
                else if(code.ToString().Length < 9)
                {
                    arg.ReplyWith(lang.GetMessage("LongerCode", this));
                }
                else if (!permission.PermissionExists(cPermission))
                {
                    arg.ReplyWith(lang.GetMessage("PermDoesNotExist", this));
                }
                
                else
                {
                    arg.ReplyWith("Success! We have made " + code + " a new code players can use to get the permission: " + cPermission + "!");
                    if (Config["codes"] == null)
                    {
                        Config["codes"] = code.ToString();

                    } else
                    {
                        Config["codes"] = Config["codes"] + ", " + code;
                    }
                       
                    Config[code.ToString()] = cPermission;
                    Config[code.ToString() + "_USES"] = 0;
                    SaveConfig();
                }
            } else
            {
                arg.ReplyWith(lang.GetMessage("ValidArgsConsole",this));
            }

        }

        [ChatCommand("refer")]
        void referCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "referral.use"))
            {
                SendReply(player, lang.GetMessage("NoPermission", this));
            }
            else
            {

                 if (args.Length == 1)
                {
                    BasePlayer p = BasePlayer.Find(args[0]);
                    if (p != null) //&& args[0] != player.displayName)
                    {
                        if (Config[player.UserIDString] != null)
                        {
                            SendReply(player, lang.GetMessage("AlreadyReferred", this));
                        } else
                        {
                            permission.GrantUserPermission(p.UserIDString, Config["GrantedPermission"].ToString(), this);
                            SendReply(player, lang.GetMessage("SuccessMessage", this), p.displayName);
                            SendReply(p, lang.GetMessage("ThankYouMessage", this), player.displayName);
                            Config[player.UserIDString] = "USED_THEIR_REFERRAL";
                            SaveConfig();
                        }
                       
                        
                    }
                    else
                    {
                        long code;
                        if (long.TryParse(args[0], out code))
                        {
                            if(Config[player.UserIDString + code.ToString()] != null)
                            {
                                SendReply(player, lang.GetMessage("CodeAlreadyUsed", this));
                            }
                            else if(Config[code.ToString()] != null)
                            {
                                permission.GrantUserPermission(player.UserIDString, Config[code.ToString()].ToString(), this);
                                SendReply(player, lang.GetMessage("SuccessMessage_2", this), code.ToString());
                                Config[player.UserIDString + code.ToString()] = true;
                                int currentUses = Convert.ToInt32(Config[code.ToString() + "_USES"]);
                                currentUses++;
                                Config[code.ToString() + "_USES"] = currentUses.ToString();
                                SaveConfig();
                            } else
                            {
                                SendReply(player, lang.GetMessage("ValidCode", this));
                            }
                        }
                        else
                        {
                            SendReply(player, lang.GetMessage("ValidArgs", this));
                        }
                    }
                }
                else
                {
                    SendReply(player, lang.GetMessage("ValidArgs", this));
                }
            }
        }

    }
}