using System;
using System.Collections.Generic;

public static class UUID
{
    private static List<Int32> idList = new List<Int32>();
    private static Random rng = new Random();
    private static int maxIDS = 10000;
    public static void PutBack(int Val)
    {
        if (!idList.Contains(Val) && Val <= maxIDS)
            idList.Add(Val);
    }
    private static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
    private static int index = 0;
    public static void InitializeUUIDS()
    {
        if (idList.Count < 1){
            for (int i = 0; i < maxIDS; i++)
            {
                idList.Add(i);
            }
            idList.Shuffle();
        }
    }
    public static int getRandomUUID()
    {
        if (index < idList.Count)
            return idList[index++];
        else
            throw new Exception("Out of UUIDS");
    }
}
