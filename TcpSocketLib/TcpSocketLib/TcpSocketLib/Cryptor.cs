using System.Security.Cryptography;

namespace TcpSocketLib
{
    public class Cryptor
    {
        public byte[] Key { get; protected set; }
        public byte[] IV { get; protected set; }

        ICryptoTransform encryptor;
        ICryptoTransform decryptor;
        

        public Cryptor(byte[] Key, byte[] IV) {
            using(RijndaelManaged rm = new RijndaelManaged()) {
                encryptor = rm.CreateEncryptor(Key, IV);
                decryptor = rm.CreateDecryptor(Key, IV);
                this.Key = Key;
                this.IV = IV;
            }
        }

        public Cryptor() {
            using (RijndaelManaged rm = new RijndaelManaged()) {
                rm.GenerateKey();
                rm.GenerateIV();
                encryptor = rm.CreateEncryptor(rm.Key, rm.IV);
                decryptor = rm.CreateDecryptor(rm.Key, rm.IV);
                this.Key = rm.Key;
                this.IV = rm.IV;
            }
        }

        public byte[] Encrypt(byte[] data) {
            return encryptor.TransformFinalBlock(data, 0, data.Length);
        }
        public byte[] Decrypt(byte[] data) {
            return decryptor.TransformFinalBlock(data, 0, data.Length);
        }
    }
}
