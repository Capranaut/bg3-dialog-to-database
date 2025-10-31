using LSLib.Granny;
using LSLib.Granny.GR2;
using LSLib.LS;
using LSLib.LS.Enums;
using LSLib.LS.Story;
using LSLib.LS.Story.HeaderParser;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static bg3dialog2db.Json;

namespace bg3dialog2db
{
    static public class Que //Database insertion queuing functions
    {

        //Null check (also flattens all 0 UUID and larian empty messages to null)
        static bool Forbidden(string ValueString)
        {
            if (ValueString == null)
            {
                return true;
            }

            foreach (string BadString in Params.NullStringArray)
            {
                if (ValueString == BadString)
                {
                    return true;
                }
            }

            return false;
        }

        //Cue a kev-value pair for database insertion after null check and optional int conversion
        static public void AddValue(string QueKey, string QueVal, bool QueInt = false, bool QueBool = false)
        {
            if (Forbidden(QueVal))
            {
                BgSQLite.LoadParam(QueKey);//Load null
            }
            else if (QueInt)
            {
                BgSQLite.LoadParam(QueKey, int.Parse(QueVal));
            }
            else if (QueBool)
            {//Problem for the future
                BgSQLite.LoadParam(QueKey, bool.Parse(QueVal));
            }
            else
            {
                BgSQLite.LoadParam(QueKey, QueVal);
            }
        }

        //Cue a key-value pair for database insertion from an XML node attribute after null check and optional int conversion from direct XML read
        static public void PropertyValue(XmlNode QueNode, string QueAttributeName, bool QueInt = false, bool QueBool = false)
        {
            XmlNode ValNode = QueNode.Attributes.GetNamedItem(QueAttributeName);
            if (ValNode != null)
            {
                AddValue(QueAttributeName, ValNode.Value, QueInt, QueBool);
            }
        //blank handeling
        return;
        }

        //Cue a key-value pair for database insertion from an XML node attribute after null check and optional int conversion from dict read
        static public void PropertyValue(Dictionary<string, string> QueNodeDict, string QueAttributeName, bool QueInt = false, bool QueBool = false)
        {
            if (QueNodeDict.TryGetValue(QueAttributeName, out string QueAttributeValue))
            {
                AddValue(QueAttributeName, QueAttributeValue, QueInt, QueBool);
                return;
            }
            BgSQLite.LoadNull(QueAttributeName);
        }

        //Cue a batch of key-value pairs for database insertion from XML node attributes after null check from direct XML read - all same type
        static public void BatchPropertyValues(XmlNode QueNode, string[] QueAttributes, bool QueInts = false, bool QueBools = false)
        {
            foreach (string QueAttribute in QueAttributes)
            {
                PropertyValue(QueNode, QueAttribute, QueInts, QueBools);
            }
        }

        //Cue a batch of key-value pairs for database insertion from XML node attributes after null check from dict read - all same type
        static public void BatchPropertyValues(Dictionary<string, string> QueNodeDict, string[] QueAttributes, bool QueInts = false, bool QueBools = false)
        {
            foreach (string QueAttribute in QueAttributes)
            {
                PropertyValue(QueNodeDict, QueAttribute, QueInts, QueBools);
            }
        }
    }

    static public class Parse //Larain Gamefile Parser
    {
        static bool NodeIs(XmlNode Node, String TargetID)//Private check function. If it returns false in this context, somebody fucked up
        {
            return (Node.Attributes.GetNamedItem("id").Value == TargetID);
        }

        static XmlNode Descend(XmlNode ParentNode)
        {
            return ParentNode.SelectSingleNode("children");
        }

        static void IngestMeta(XmlNode SubNode, string TagUUID, string Source)
        {
                string TagID = SubNode.Attributes.GetNamedItem("id").Value;//Uncaught, becuase if there's something there, surely it has an ID right?
                XmlNode SubNodePayload = SubNode.FirstChild;

                if (!NodeIs(SubNodePayload, "Name") && !NodeIs(SubNodePayload, TagID))
                {
                    throw new ArgumentException("Node passed to Parse.Meta contains an attribute other than name");
                }

                BgSQLite.LoadCom("INSERT or REPLACE INTO Meta VALUES (@UUID,@id,@value,@Source)");
                Que.AddValue("UUID", TagUUID);
                Que.AddValue("id", TagID);
                Que.AddValue("Source", Source);
                Que.PropertyValue(SubNodePayload, "value");
                BgSQLite.ExecuteNonQuery();
        }

        static void MetaManager(XmlNode MetasNode, string TagUUID, string Source)
        {
            if (MetasNode == null || !MetasNode.HasChildNodes)
            {
                return;//TODO Some Logging. That specific file didn't have any
            }

            foreach (XmlNode MetaNode in MetasNode.ChildNodes)
            {
                if (MetaNode.FirstChild == null)
                {
                    return;//TODO log empy cat
                }

                foreach (XmlNode SubNode in MetaNode.FirstChild.ChildNodes)
                {
                    IngestMeta(SubNode, TagUUID, Source);
                }
            }

        }

        static public void IngestTags(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML_Conditional(pakFile);
            XmlNode TagNode = Fetch.Node(pakDoc, "/save/region/node");
            if (!NodeIs(TagNode, "Tags"))
            {
                throw new ArgumentException("Node passed to Parse.Tags is not a Tag node");
            }
            BgSQLite.LoadCom("INSERT or REPLACE INTO Tags VALUES (@UUID,@Name,@DisplayName,@DisplayDescription,@Icon,@Description,@Source)");
            Dictionary<string, string> TagDict = Fetch.Properties(TagNode);
            Que.BatchPropertyValues(TagDict, new string[] { "UUID", "Name", "DisplayName", "DisplayDescription", "Icon", "Description" });
            Que.AddValue("Source", pakFile.Name);
            BgSQLite.ExecuteNonQuery();

            MetaManager(Descend(TagNode), TagDict["UUID"], pakFile.Name);
        }

        static public void IngestFlags(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML_Conditional(pakFile);
            XmlNode FlagNode = Fetch.Node(pakDoc);
            if (!NodeIs(FlagNode, "Flags"))
            {
               // throw new ArgumentException("Node passed to Parse.Flags is not a Flags node");//Throws 6 on a good run. Disabling so I can catch better TODO reenable
            }
            BgSQLite.LoadCom("INSERT or REPLACE INTO Flags VALUES (@UUID,@Name,@Description,@Usage,@Source)");
            Dictionary<string, string> FlagDict = Fetch.Properties(FlagNode);
            Que.BatchPropertyValues(FlagDict, new string[] { "UUID", "Name", "Description" });//str
            Que.BatchPropertyValues(FlagDict, new string[] { "Usage" }, true);//int
            Que.AddValue("Source", pakFile.Name);
            BgSQLite.ExecuteNonQuery();
        }

        static public void IngestQuestSteps(XmlNode StepsNode, string QuestUUID, string Source)
        {
            if (StepsNode == null || !StepsNode.HasChildNodes)
            {
                throw new ArgumentException("Stepless quest");
            }
            foreach (XmlNode SubNode in StepsNode.ChildNodes)
            {
                if (NodeIs(SubNode, "SubQuests"))
                {
                    IngestMeta(SubNode, QuestUUID, Source);
                }

                else if (!NodeIs(SubNode, "QuestStep"))
                {
                    throw new ArgumentException("Node passed to Parse.QuestStep is invalid");
                }

                else
                {
                    BgSQLite.LoadCom("INSERT or REPLACE INTO QuestSteps VALUES (@QuestStepGuid,@QuestUUID,@Achievement,@Description," +
                        "@DevComment,@DialogFlagGUID,@ExperienceReward,@ID,@LevelOverride,@Objective,@QuestRewardCount," +
                        "@QuestRewardLevel,@QuestTitleOverride,@ReputationGain,@RewardAdditionalGold,@RewardAdditionalOwnerGUID," +
                        "@RewardAdditionalOwnerLevelName,@RewardAdditionalOwnerName,@RewardAdditionalTreasureTable," +
                        "@StatTriggerGUID,@UnlockDisable,@Source)");

                    Dictionary<string, string> QuestDict = Fetch.Properties(SubNode);
                    Que.BatchPropertyValues(QuestDict, new string[] {"QuestStepGuid", "Achievement", "Description",
                    "DevComment", "DialogFlagGUID", "ExperienceReward","ID", "Objective", "QuestTitleOverride",
                    "RewardAdditionalGold", "RewardAdditionalOwnerGUID", "RewardAdditionalOwnerLevelName",
                    "RewardAdditionalOwnerName", "RewardAdditionalTreasureTable", "StatTriggerGUID"});//Str
                    Que.BatchPropertyValues(QuestDict, new string[] { "LevelOverride", "QuestRewardCount",
                    "QuestRewardLevel", "ReputationGain", "UnlockDisable"}, true);//Int

                    Que.AddValue("QuestUUID", QuestUUID);
                    Que.AddValue("Source", Source);

                    BgSQLite.ExecuteNonQuery();
                }
            }
        }

        static public void IngestQuests(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode QuestNodes = Fetch.Node(pakDoc, "/save/region/node");
            if (!NodeIs(QuestNodes, "root"))
            {
                throw new ArgumentException("Node passed to Parse.Quests does not start at a root node");
            }

            foreach (XmlNode QuestNode in QuestNodes.FirstChild.ChildNodes)
            {
                if (!NodeIs(QuestNode, "Quest"))
                {
                    throw new ArgumentException("Node passed to Parse.Quests contains a non-quest node");
                }

                BgSQLite.LoadCom("INSERT or REPLACE INTO Quests VALUES (@QuestGuid,@QuestID,@QuestTitle," +
                    "@CategoryID,@ParentQuestID,@QuestVisiblity,@QuestRewardTarget,@SortingPriority,@Source)");

                Dictionary<string, string> QuestDict = Fetch.Properties(QuestNode);
                Que.BatchPropertyValues(QuestDict, new string[] { "QuestGuid", "QuestID", "QuestTitle",
                "CategoryID", "ParentQuestID"});//Str
                Que.BatchPropertyValues(QuestDict, new string[] { "SortingPriority", "QuestRewardTarget" }, true);//Int
                Que.BatchPropertyValues(QuestDict, new string[] { "QuestVisiblity" }, false, true);//Bool
                Que.AddValue("Source", pakFile.Name);
                BgSQLite.ExecuteNonQuery();

                IngestQuestSteps(Descend(QuestNode), QuestDict["QuestGuid"], pakFile.Name);
            }
        }

        static public void IngestReactions(XmlNode reactNode, Dictionary<string,string> ParrentDict, string Source)
        {
            if (!NodeIs(reactNode, "Reaction"))
            {
                throw new ArgumentException("Node passed to Parse.Reactions is not a Reaction node");
            }
            BgSQLite.LoadCom("INSERT or REPLACE INTO Reactions VALUES (@UUID,@id,@value,@Scope,@Source)");
            Dictionary<string, string> ReactDict = Fetch.Properties(reactNode);
            Que.PropertyValue(ReactDict, "id");//str
            Que.PropertyValue(ReactDict, "value", true);//int
            Que.PropertyValue(ParrentDict, "UUID");//str
            Que.PropertyValue(ParrentDict, "Scope", true);//int
            Que.AddValue("Source", Source);
            BgSQLite.ExecuteNonQuery();
        }

        static public void IngestReactions(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode ReactNode = Fetch.Node(pakDoc, "/save/region/node/children/node");
            if (!NodeIs(ReactNode, "Reaction"))
            {
                throw new ArgumentException("Node passed to Parse.Reactions is not a Reaction node");
            }
            Dictionary<string, string> ReactDict = Fetch.Properties(ReactNode);

            XmlNode MidNode = Descend(ReactNode);

            if (MidNode == null)
            {
                BgSQLite.LoadCom("INSERT or REPLACE INTO Reactions VALUES (@UUID,null,null,@Scope,@Source)");
                Que.PropertyValue(ReactDict, "UUID");//str
                Que.PropertyValue(ReactDict, "Scope", true);//int
                Que.AddValue("Source", pakFile.Name);
                BgSQLite.ExecuteNonQuery();

                return; //some just seem to be blank: a52602f6-61ec-413b-a6c3-991d2e451a13
            }

            if (MidNode.ChildNodes.Count != 1 || !NodeIs(MidNode.FirstChild, "Reactions"))
            {
                throw new ArgumentException("Node passed to Parse.Reactions has unexpected format");
            }

            foreach (XmlNode SubNode in MidNode.FirstChild.FirstChild.ChildNodes)
            {
                IngestReactions(SubNode, ReactDict, pakFile.Name);
            }
        }

        static public void IngestDC(PackagedFileInfo pakFile)//come back later for strickter checks across the board and hopefully broader scraping built off region id checks
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode DiffNode = Fetch.Node(pakDoc, "/save/region/node/children");

            foreach (XmlNode DCNode in DiffNode.ChildNodes)
            {
                if (!NodeIs(DCNode, "DifficultyClass"))
                {
                    throw new ArgumentException("Node passed to Parse.DC is not a DC node");
                }
                BgSQLite.LoadCom("INSERT or REPLACE INTO DC VALUES (@UUID,@Name,@Difficulties,@Source)");
                Dictionary<string, string> DCDict = Fetch.Properties(DCNode);
                Que.BatchPropertyValues(DCDict, new string[] { "UUID", "Name", "Difficulties" });//Don't ask my why Difficulties is str
                Que.AddValue("Source", pakFile.Name);
                BgSQLite.ExecuteNonQuery();
            }
        }

        static public void IngestNamesMerged(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML_Conditional(pakFile);
            XmlNode NameMerdNode = Fetch.Node(pakDoc);
        }

        static public void IngestNamesOrigins(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode NameOriNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestSpeakerGroups(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML(pakFile);
            XmlNode SpeakGroupNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestAudio(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML(pakFile);
            XmlNode AudNode = Fetch.Node(pakDoc, "/save/region/node/children/node/children/node/children");
        }

        static public void IngestLang(PackagedFileInfo pakFile)//Called sepeartly from switch
        {
            using var pakStream = pakFile.CreateContentReader();
            var fileExt = LocaUtils.ExtensionToFileFormat(pakFile.Name);
            var locReader = LocaUtils.Load(pakStream, fileExt);

            BgSQLite.LoadCom("INSERT or REPLACE INTO Localization VALUES (@UUID,@Line)");
            foreach (var entry in locReader.Entries)
            {
                BgSQLite.LoadParam("UUID", entry.Key);
                BgSQLite.LoadParam("Line", entry.Text);
                BgSQLite.ExecuteNonQuery();
            }
        }

        static public void IngestSwitch(PackagedFileInfo pakFile)
        {
            if (PakTest.IsTag(pakFile))
                IngestTags(pakFile);
            else if (PakTest.IsFlag(pakFile))
                IngestFlags(pakFile);
            else if (PakTest.IsQuest(pakFile))
                IngestQuests(pakFile);
            else if (PakTest.IsReaction(pakFile))
                IngestReactions(pakFile);
            else if (PakTest.IsDC(pakFile))
                IngestDC(pakFile);
            else if (PakTest.IsMerged(pakFile))
                IngestNamesMerged(pakFile);
            else if (PakTest.IsOrigin(pakFile))
                IngestNamesOrigins(pakFile);
            else if (PakTest.IsSpeakerGroup(pakFile))
                IngestSpeakerGroups(pakFile);
            else if (PakTest.IsAudio(pakFile))
                IngestAudio(pakFile);
            else
                return;//Just to make it explicit
        }

        static public void IngestPak(string pakFilePath)
        {
            BgSQLite.Begin();
            PackageReader pakReader = new();//Invoke LsLib
            using Package pak = pakReader.Read(pakFilePath);
            foreach (PackagedFileInfo pakFile in pak.Files)
            {
                IngestSwitch(pakFile);//Error handeling is for pussies. TODO fix later
                /*try
                {
                    IngestSwitch(pakFile);//Ingest offloaded for error handeling and potential future parallelization
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error ingesting {pakFile.Name} from {Path.GetFileName(pakFilePath)}: {e.Message}");
                }*/
            }
            BgSQLite.End();
        }

        static public void IngestLocalization(string GamePath, string locLang)
        {
            BgSQLite.Begin();
            string langPakPath = $"{GamePath}\\Localization\\{locLang}\\{locLang}.pak";
            if (locLang == "English")//Larian just had to have an edge case
                langPakPath = $"{GamePath}\\Localization\\English.pak";

            PackageReader pakReader = new();//Invoke LsLib
            using Package pak = pakReader.Read(langPakPath);
            foreach (PackagedFileInfo pakFile in pak.Files)
            {
                if (PakTest.IsLang(pakFile, locLang))
                {
                    try
                    {
                        IngestLang(pakFile);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error ingesting {pakFile.Name} from {Path.GetFileName(langPakPath)}: {e.Message}");
                    }
                }
            }
            
        }
    }
}
