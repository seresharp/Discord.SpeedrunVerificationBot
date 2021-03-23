using System;
using System.Threading.Tasks;

namespace VerificationBot.BackgroundTasks
{
    public abstract class BackgroundTask
    {
        public virtual TimeSpan Delay => TimeSpan.Zero;
        public bool IsRunning => _task != null && !_task.IsCompleted;

        private Task _task;
        private TimeSpan _prevTime;

        public bool TryStartTask(VerificationBot bot)
        {
            if (IsRunning || (_prevTime + Delay).TotalMilliseconds > Environment.TickCount64)
            {
                return false;
            }

            _task = Task.Run(() => Run(bot));
            _prevTime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return true;
        }

        protected abstract Task Run(VerificationBot bot);
    }
}
