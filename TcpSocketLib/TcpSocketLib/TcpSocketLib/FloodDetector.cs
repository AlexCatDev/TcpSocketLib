namespace TcpSocketLib
{
    public class FloodDetector
    {
        public int Receives { get; set; }
        public int Delta { get; set; }

        public FloodDetector(int Receives, int Delta) {
            this.Receives = Receives;
            this.Delta = Delta;
        }
    }
}
