namespace Model
{
    public class Event
    {
        public Team Team { get; set; }

        public int Minute { get; set; }

        //Per ora sono tutti goal, ignoro questa prop
        public string EventType { get; } 
    }
}