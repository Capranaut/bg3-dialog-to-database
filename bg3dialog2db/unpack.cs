using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//I miss dynamic typing in python. Sue me

namespace bg3dialog2db
{
    public static class Unpack
    {
        public static string One(string[] OneString)
        {
            return OneString[0];
        }

        public static (string, string) Two(string[] TwoString)
        {
            return (TwoString[0], TwoString[1]);
        }

        public static (string, string, string) Three(string[] ThreeString)
        {
            return (ThreeString[0], ThreeString[1], ThreeString[2]);    
        }

        public static (string, string, string, string) Four(string[] FourString)
        {
            return (FourString[0], FourString[1], FourString[2], FourString[3]);
        }

        public static (string, string, string, string, string) Five(string[] FiveString)
        {
            return (FiveString[0], FiveString[1], FiveString[2], FiveString[3], FiveString[4]);
        }

        public static (string, string, string, string, string, string) Six(string[] SixString)
        {
            return (SixString[0], SixString[1], SixString[2], SixString[3], SixString[4], SixString[5]);
        }

        public static (string, string, string, string, string, string, string) Seven(string[] SevenString)
        {
            return (SevenString[0], SevenString[1], SevenString[2], SevenString[3], SevenString[4], SevenString[5], SevenString[6]);
        }

        public static (string, string, string, string, string, string, string, string) Eight(string[] EightString)
        {
            return (EightString[0], EightString[1], EightString[2], EightString[3], EightString[4], EightString[5], EightString[6], EightString[7]);
        }

        public static (string, string, string, string, string, string, string, string, string) Nine(string[] NineString)
        {
            return (NineString[0], NineString[1], NineString[2], NineString[3], NineString[4], NineString[5], NineString[6], NineString[7], NineString[8]);
        }

        public static (string, string, string, string, string, string, string, string, string, string) Ten(string[] TenString)
        {
            return (TenString[0], TenString[1], TenString[2], TenString[3], TenString[4], TenString[5], TenString[6], TenString[7], TenString[8], TenString[9]);
        }
    }
}
