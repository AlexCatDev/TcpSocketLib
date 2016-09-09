namespace TcpSocketLib
{
    public class FloodProtector
    {
        public int MaxReceives { get; set; }
        public int Delta { get; set; }

        public FloodProtector(int MaxPackets, int Time) {
            this.MaxReceives = MaxPackets;
            this.Delta = Time;
        }
    }
}
