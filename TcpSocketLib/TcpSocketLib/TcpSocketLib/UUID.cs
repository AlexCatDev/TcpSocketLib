using System;
using System.Collections.Generic;

namespace TcpSocketLib
{
    public static class UUID
    {
        private static List<Int32> idList = new List<Int32>();
        private static Random rng = new Random();
        public static void PutBack(int Val) {
            if (!idList.Contains(Val))
                idList.Add(Val);
        }
        private static int index = 0;
        public static void InitializeUUIDS<T>(int start, int end) {
            if (idList.Count < 1) {
                for (int i = start; i < end; i++) {
                    idList.Add(i);
                }
                idList.Shuffle();
            }
        }
        public static int getRandomUUID() {
            if (index < idList.Count)
                return idList[index++];
            else
                throw new Exception("Out of UUIDS");
        }
    }
}
