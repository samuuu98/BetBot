using System;
using System.Collections.Generic;
using System.Text;

namespace Model
{
    public class Result
    {
        public int Home { get; set; }
        public int Away { get; set; }

        public int HomeCorner { get; set; }
        public int AwayCorner { get; set; }

        public int HomeShot { get; set; }
        public int AwayShot { get; set; }

        public int HomeShotOnTarget { get; set; }
        public int AwayShotOnTarget { get; set; }

        public int HomeShotOutside { get; set; }
        public int AwayShotOutside { get; set; }

        public int Minutes { get; set; }

        public List<int> GoalHome { get; set; }
        public List<int> GoalAway { get; set; }

        public Result()
        {
            GoalHome = new List<int>();
            GoalAway = new List<int>();
        }
    }
}