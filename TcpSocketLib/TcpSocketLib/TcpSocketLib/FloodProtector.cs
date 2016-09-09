namespace TcpSocketLib
{
    public class FloodProtector
    {
        public int MaxReceives { get; set; }
        public int Delta { get; set; }

        public FloodProtector(int MaxReceives, int Time) {
            this.MaxReceives = MaxReceives;
            this.Delta = Time;
        }
    }
}
