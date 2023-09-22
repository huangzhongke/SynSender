using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jst.SynchubClient.Cosco
{
    public class TickTimer
    {
        public static TickTimer Instance { get; private set; } = new TickTimer();
        private TickTimer()
        {
            _ = tick();
        }



        public Action<DateTime> Event;
        /*
         *
         *(DateTime)=>{
         *  222
         * }
         * (DateTime)=>{
         *  11
         * }
         */
        const int TICK_TIME = 50;
        private async Task tick()
        {
            while (true)
            {
                await Task.Delay(TICK_TIME);
                _ = Task.Run(() => {
                    Event?.Invoke(DateTime.Now);
                });
            }
        }

    }
}
