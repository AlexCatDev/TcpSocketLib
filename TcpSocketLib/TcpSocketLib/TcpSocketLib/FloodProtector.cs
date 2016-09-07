namespace TcpSocketLib
{
    public class FloodProtector
    {
        public int MaxPackets { get; set; }
        public int Time { get; set; }

        public FloodProtector(int MaxPackets, int Time) {
            this.MaxPackets = MaxPackets;
            this.Time = Time;
        }
    }
}
