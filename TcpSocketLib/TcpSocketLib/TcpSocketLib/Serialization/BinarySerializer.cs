using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpSocketLib.Serialization
{
    /// <summary>
    /// UNFINISHED
    /// </summary>
    public class BinarySerializer
    {
        public BinarySerializer() {

        }

        public byte[] Serialize<T>(T obj) {
            //Get type  
            Type type = typeof(T);

            //Use a memory stream to store the data
            using (MemoryStream ms = new MemoryStream()) {
                //Use a binary writer to write the objects to memory stream
                using (BinaryWriter bw = new BinaryWriter(ms)) {
                    //Scan all public properties
                    foreach (var property in type.GetProperties()) {
                        //Get the value
                        object value = property.GetValue(obj, null);
                        //Get the type
                        Type valueType = value.GetType();

                        //Check the type and write to stream appropriately
                        if(valueType == typeof(int)) {
                            bw.Write((int)value);
                        } else if(valueType == typeof(bool)) {
                            bw.Write((bool)value);
                        }
                        
                    }

                    return ms.ToArray();
                }
            }
        }

        public T Deserialize<T>(byte[] data) {
            //Get type  
            Type type = typeof(T);

            List<object> objectTree = new List<object>();

            foreach (var property in type.GetProperties()) {
                //Get the type
                Type valueType = property.GetType();
            }

            return default(T);
        }
    }
}
