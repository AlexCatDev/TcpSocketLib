using System;
using System.Collections.Generic;

namespace TcpSocketLib
{
    public static class UUID
    {
        private static List<Int32> idList = new List<Int32>();
        private static Random rng = new Random();

        private static int index = 0;

        public static void PutBack(int value) {
            if (!idList.Contains(value))
                idList.Add(value);
        }

        public static void InitializeUUIDS(int start, int end, bool shuffle) {
            if (idList.Count < 1) {
                for (int i = start; i < end; i++) {
                    idList.Add(i);
                }
                if(shuffle)
                idList.Shuffle<int>();
            }
        }

        public static int GetUniqueIdentifier() {
            if (index <= idList.Count)
                return idList[index++];
            else
                throw new Exception("Out of unique identifiers");
        }
    }
}
