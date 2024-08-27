using System;
using System.Collections.Generic;
using System.Text;

namespace Model
{
    public class Match
    {
        public Competition Competition { get; set; }
        public DateTime Date { get; set; }

        public string Home{ get; set; }
        public string Away { get; set; }

        public Result Result { get; set; }

        public void Update(int homeRes, int awayRes, int min) 
        {
            Result.Home = homeRes;
            Result.Away = awayRes;
            Result.Minutes = min;
        }

        public override bool Equals(object obj)
        {
            if (obj is Match match)
                return Date == match.Date && Home == match.Home && Away == match.Away;

            return false;
        }

        public static bool operator ==(Match lhs, Match rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator !=(Match lhs, Match rhs) => !(lhs == rhs);
    }
}
