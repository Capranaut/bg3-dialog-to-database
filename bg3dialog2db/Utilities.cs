using bg3dialog2db;
using LSLib.LS;
using LSLib.LS.Enums;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static bg3dialog2db.Json;

namespace bg3dialog2db
{
    public static class Params //Program parameters set at compile time
    {
        public static readonly string dbFilename = "BG3 Dialog Database.db";//In final build shift params from database handler here
        public static readonly string dbConnectionString = $"Data Source={dbFilename}";

        public static readonly string[] NullStringArray = ["", " ", "NULL", "null", "Null", "-1", 
            "ls::TranslatedStringRepository::s_HandleUnknown",
            "00000000-0000-0000-0000-000000000000"];
    }

    public static class PakTest
    {
        public static bool IsTag(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/Tags/") && pakFile.Name.Contains(".ls"));
        }

        public static bool IsFlag(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/Flags/") && pakFile.Name.Contains(".ls"));
        }

        public static bool IsQuest(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("//Story/Journal/quest_prototypes.lsx"));
        }

        public static bool IsReaction(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/ApprovalRatings/Reactions/"));
        }

        public static bool IsDC(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/DifficultyClasses/DifficultyClasses.lsx"));
        }

        public static bool IsItemMerged(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/Items/_merged.lsf"));
        }

        public static bool IsCharMerged(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/Characters/_merged.lsf"));
        }

        public static bool IsRootMerged(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/RootTemplates/") && !pakFile.Name.Contains("/Content/"));
        }

        public static bool IsMerged(PackagedFileInfo pakFile)
        {
            return (IsItemMerged(pakFile) || IsCharMerged(pakFile) || IsRootMerged(pakFile));
        }

        public static bool IsOrigin(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/Origins/Origins.lsx"));
        }

        public static bool IsSpeakerGroup(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/Voice/SpeakerGroups.lsf"));
        }

        public static bool IsAudio(PackagedFileInfo pakFile)
        {
            return (pakFile.Name.Contains("/Localization/English/Soundbanks/") && pakFile.Name.Contains(".lsf"));
        }

        public static bool IsLang(PackagedFileInfo pakFile, string locLang = "English")
        {
            return (
                pakFile.Name.Contains($"/{locLang}/{locLang.ToLower()}.loca") || 
                pakFile.Name.Contains($"/{locLang}/{locLang.ToLower()}.xml"));
        }

        public static bool IsParsable(PackagedFileInfo pakFile)
        {
            return (
                IsTag(pakFile) || 
                IsFlag(pakFile) || 
                IsQuest(pakFile) ||
                IsReaction(pakFile) || 
                IsDC(pakFile) || 
                IsMerged(pakFile) || 
                IsOrigin(pakFile) || 
                IsSpeakerGroup(pakFile) ||
                IsAudio(pakFile));
        }

        public static string IsWhich(PackagedFileInfo pakFile)//For logging purposes
        {
            if (IsTag(pakFile))
                return "IsTag";
            else if (IsFlag(pakFile))
                return "IsFlag";
            else if (IsQuest(pakFile))
                return "IsQuest";
            else if (IsReaction(pakFile))
                return "IsApproval";
            else if (IsDC(pakFile))
                return "IsDC";
            else if (IsItemMerged(pakFile))
                return "IsItemMerged";
            else if (IsCharMerged(pakFile))
                return "IsCharMerged";
            else if (IsRootMerged(pakFile))
                return "IsRootMerged";
            else if (IsOrigin(pakFile))
                return "IsOrigin";
            else if (IsSpeakerGroup(pakFile))
                return "IsSpeakerGroup";
            else if (IsAudio(pakFile))
                return "IsAudio";
            else if (IsLang(pakFile))
                return "IsLang";
            return "IsNone";
        }
    }

    public static class Fetch
    {
        /* This should really be done with linq or something, but I'm trying to maintain the legacy code in parallel, so hacks it is
         * The upside is that I can flatten value/handle to a single parameter in the dictionary, and eliminate half the cue functions
         * Find all attribute tags, return id-val pair, where val is either value or handle attribute*/
            static public Dictionary<string, string> Properties(XmlNode RootNode)
            {
                Dictionary<string, string> OutDict = new();
                string key;
                string val;
                XmlAttributeCollection guts;

                foreach (XmlNode ChildNode in RootNode.ChildNodes)
                {
                    if (ChildNode.Name == "attribute")
                    {
                        guts = ChildNode.Attributes;

                        key = guts.GetNamedItem("id").Value;
                        val = guts.GetNamedItem("value").Value;

                        if (val is null)
                            val = guts.GetNamedItem("handle").Value;

                        OutDict[key] = val;
                    }
                }
                return OutDict;
            }

        static void WriteToLSX(Resource _Resource, MemoryStream _MemoryStream, ResourceFormat _Format, ResourceConversionParameters _Params)
        {//No god damn clue what this does, or how it works. renamed from rConvert, and gave better variable names
            LSXWriter _LSXWriter = new(_MemoryStream);
            _LSXWriter.Version = _Params.LSX;
            _LSXWriter.PrettyPrint = _Params.PrettyPrint;
            _LSXWriter.Write(_Resource);
        }

        //I think the duplicated start and end chunks in the next three subroutines are required for memory managment through "using" statements
        public static XmlDocument LSX_XML_Conditional(PackagedFileInfo pakFile)
        {
            using Stream fileStream = pakFile.CreateContentReader();
            using MemoryStream pakStream = new();

            if (pakFile.Name.Contains(".lsf"))
            {
                using LSFReader _LSFReader = new(fileStream);
                WriteToLSX(
                    _LSFReader.Read(),
                    pakStream,
                    ResourceFormat.LSX,
                    ResourceConversionParameters.FromGameVersion(Game.BaldursGate3));
            }
            else if (pakFile.Name.Contains(".lsx"))
            {
                fileStream.CopyTo(pakStream);
            }

            pakStream.Position = 0;

            using XmlReader pakRead = XmlReader.Create(pakStream);
            XmlDocument pakDoc = new();
            pakDoc.Load(pakRead);
            return pakDoc;
        }

        public static XmlDocument LSX_XML(PackagedFileInfo pakFile)
        {
            using Stream fileStream = pakFile.CreateContentReader();
            using MemoryStream pakStream = new();
            using LSFReader _LSFReader = new(fileStream);

            WriteToLSX(
                _LSFReader.Read(),
                pakStream,
                ResourceFormat.LSX,
                ResourceConversionParameters.FromGameVersion(Game.BaldursGate3));

            pakStream.Position = 0;

            using XmlReader pakRead = XmlReader.Create(pakStream);
            XmlDocument pakDoc = new();
            pakDoc.Load(pakRead);
            return pakDoc;
        }

        public static XmlDocument XML(PackagedFileInfo pakFile)
        {
            using Stream pakStream = pakFile.CreateContentReader();
            using XmlReader pakRead = XmlReader.Create(pakStream);
            XmlDocument pakDoc = new();
            pakDoc.Load(pakRead);
            return pakDoc;
        }

        public static XmlNode Node(XmlDocument pakDoc, string nodePath = "/save/region/node")
        {
            return pakDoc.DocumentElement.SelectSingleNode(nodePath);
        }
        

    }


    public static class Utilities //phase out
    {

        public static bool Truthy(bool[] TestArray)
        {
            foreach (bool entry in TestArray)
            {
                if (!entry)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool NodeIdIs(XmlNode Node, String SearchID)
        {
            return (Node.Attributes.GetNamedItem("id").Value == SearchID);
        }

        public static bool AttributeIdIs(XmlNode Node, String SearchID)
        {
            return (Node.Name == "attribute" && NodeIdIs(Node, SearchID));
        }

        

        public static string[] GetNodeVal(XmlNodeList Nodes, string[] Find, string[] Get, bool forceAll = false, bool forceUnique = false)
        {
            int len = Find.Length;
            string[] values = new string[len];
            bool[] flags = new bool[len];

            foreach (XmlNode Node in Nodes)
            {
                for (int i = 0; i < len; i++)
                {
                    if (AttributeIdIs(Node, Find[i]))
                    {   
                        if (flags[i] && forceUnique)
                        {
                            throw new Exception($"NodeGetParams found multiple entries for: {Find[i]}");
                        }

                        flags[i] = true;
                        values[i] = Node.Attributes.GetNamedItem(Get[i]).Value;
                    }
                }
            }

            if (forceAll && !Truthy(flags))
            {
                throw new System.Collections.Generic.KeyNotFoundException("GetNode search miss with force set to true");
            }
            
            return values;
        }
        /* Until I'm sure there are cases where there couldn't be multiple hits
                public static XmlNode GetNode(XmlNodeList Nodes, string Find)
                {
                    foreach (XmlNode Node in Nodes)
                    {
                        if (NodeIdIs(Node, Find))
                        { 
                            return Node;

                        }
                    }

                    throw new System.Collections.Generic.KeyNotFoundException("GetNode search miss");
                }

                //Overload of the above function
                public static XmlNode GetNode(XmlNode Nodes, string Find)
                {
                    foreach (XmlNode Node in Nodes)
                    {
                        if (NodeIdIs(Node, Find))
                        {
                            return Node;

                        }
                    }

                    throw new System.Collections.Generic.KeyNotFoundException("GetNode search miss");
                }
        */ 
    }
}