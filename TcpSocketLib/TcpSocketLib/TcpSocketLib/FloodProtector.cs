namespace TcpSocketLib
{
    public class FloodProtector
    {
        public int MaxPackets { get; set; }
        public int OverTime { get; set; }

        public FloodProtector(int MaxPackets, int OverTime) {
            this.MaxPackets = MaxPackets;
            this.OverTime = OverTime;
        }
    }
}
