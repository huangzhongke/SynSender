using System;

namespace Jst.SynchubClient.One
{
    public class MyCommonUtil
    {   
        
        /**
         * t1 < t2 true
         * t1 > t2 false
         */
        public static bool CompareDate(DateTime t1,DateTime t2)
        {
            int res = DateTime.Compare(t1,t2);
            if (res < 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}