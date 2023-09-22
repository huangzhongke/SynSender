namespace SynchubServer.Models
{
    public class CookieModel
    {
        /// <summary>
        /// 
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string value { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string domain { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string path { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double expires { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int size { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string httpOnly { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string secure { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string session { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string sameParty { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string sourceScheme { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int sourcePort { get; set; }
    }
}