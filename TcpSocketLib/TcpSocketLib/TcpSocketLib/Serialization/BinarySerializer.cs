using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace TcpSocketLib.Serialization
{
    /// <summary>
    /// UNFINISHED
    /// </summary>

    public class BinarySerializer
    {
        public byte[] Serialize<Type>(Type obj) {
            var type = obj.GetType();

            PropertyInfo[] proberties = type.GetProperties();

            using (MemoryStream ms = new MemoryStream()) {
                using (BinaryWriter bw = new BinaryWriter(ms)) {

                    foreach (var property in proberties) {
                        var probType = property.PropertyType;
                        var value = property.GetValue(obj, null);

                        if (probType == typeof(bool)) {
                            bw.Write((bool)value);
                        }
                        else if (probType == typeof(byte)) {
                            bw.Write((byte)value);
                        }
                        else if (probType == typeof(byte[])) {
                            byte[] byteArray = (byte[])value;
                            int length = byteArray.Length;
                            bw.Write(length);
                            bw.Write(byteArray);
                        }
                        else if (probType == typeof(char)) {
                            bw.Write((char)value);
                        }
                        else if (probType == typeof(decimal)) {
                            bw.Write((decimal)value);
                        }
                        else if (probType == typeof(double)) {
                            bw.Write((double)value);
                        }
                        else if (probType == typeof(float)) {
                            bw.Write((float)value);
                        }
                        else if (probType == typeof(int)) {

                            bw.Write((int)value);
                        }
                        else if (probType == typeof(long)) {
                            bw.Write((long)value);

                        }
                        else if (probType == typeof(sbyte)) {
                            bw.Write((sbyte)value);

                        }
                        else if (probType == typeof(short)) {

                            bw.Write((short)value);
                        }
                        else if (probType == typeof(string)) {

                            bw.Write((string)value);
                        }
                        else if (probType == typeof(uint)) {
                            bw.Write((uint)value);

                        }
                        else if (probType == typeof(ulong)) {
                            bw.Write((ulong)value);

                        }
                        else if (probType == typeof(ushort)) {
                            bw.Write((ushort)value);

                        }
                    }
                    return ms.ToArray();
                }
            }
        }


        public T Deserialize<T>(byte[] data) {
            var type = typeof(T);

            List<object> objectTree = new List<object>();

            List<Type> typeMap = new List<Type>();

            foreach (var parameter in type.GetConstructors()[0].GetParameters()) {
                typeMap.Add(parameter.ParameterType);
            }

            foreach (var t in typeMap) {
                using (MemoryStream ms = new MemoryStream(data)) {
                    using (BinaryReader br = new BinaryReader(ms)) {
                        if (t == typeof(bool)) {
                            objectTree.Add(br.ReadBoolean());
                        }
                        else if (t == typeof(byte)) {
                            objectTree.Add(br.ReadByte());
                        }
                        else if (t == typeof(byte[])) {
                            int len = br.ReadInt32();
                            byte[] byteArray = br.ReadBytes(len);
                            objectTree.Add(byteArray);
                        }
                        else if (t == typeof(char)) {
                            objectTree.Add(br.ReadChar());
                        }
                        else if (t == typeof(decimal)) {
                            objectTree.Add(br.ReadDecimal());
                        }
                        else if (t == typeof(double)) {
                            objectTree.Add(br.ReadDouble());
                        }
                        else if (t == typeof(float)) {
                            objectTree.Add(br.ReadSingle());
                        }
                        else if (t == typeof(int)) {
                            objectTree.Add(br.ReadInt32());
                        }
                        else if (t == typeof(long)) {
                            objectTree.Add(br.ReadInt64());
                        }
                        else if (t == typeof(sbyte)) {
                            objectTree.Add(br.ReadSByte());
                        }
                        else if (t == typeof(short)) {
                            objectTree.Add(br.ReadInt16());
                        }
                        else if (t == typeof(string)) {
                            objectTree.Add(br.ReadString());
                        }
                        else if (t == typeof(uint)) {
                            objectTree.Add(br.ReadUInt32());
                        }
                        else if (t == typeof(ulong)) {
                            objectTree.Add(br.ReadUInt64());
                        }
                        else if (t == typeof(ushort)) {
                            objectTree.Add(br.ReadUInt16());
                        }
                    }
                }
            }

            return (T)Activator.CreateInstance(typeof(T), objectTree.ToArray());
        }

    }
}
