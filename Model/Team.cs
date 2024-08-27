using System;
using System.Collections.Generic;
using System.Text;

namespace Model
{
    public class Team
    {
        public string Name { get; set; }

        public Stats Stat { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Team team)
                return Name == team.Name;

            return false; 
        }

        public static bool operator ==(Team lhs, Team rhs)
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

        public static bool operator !=(Team lhs, Team rhs) => !(lhs == rhs);
    }
}
