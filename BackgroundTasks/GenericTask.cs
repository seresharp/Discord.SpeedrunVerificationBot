using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerificationBot.BackgroundTasks
{
    public class GenericTask : BackgroundTask
    {
        private TimeSpan _delay;
        public override TimeSpan Delay => _delay;

        private Func<VerificationBot, Task> _taskFactory;

        public GenericTask(TimeSpan delay, Func<VerificationBot, Task> taskFactory)
        {
            _delay = delay;
            _taskFactory = taskFactory;
        }

        protected override Task Run(VerificationBot bot)
            => _taskFactory(bot);
    }
}
