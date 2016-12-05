using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Model
{
    public class PFTask
    {
        private void Init()
        {
            StartTime = DateTime.UtcNow;
            TokenSource = new CancellationTokenSource();
            CancellationToken = TokenSource.Token;
        }
        public PFTask()
        {
            Init();
        }
        public PFTask(Task task)
        {
            Task = task;
            Init();
        }
        public DateTime StartTime { get; set; }
        public Task Task { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public CancellationTokenSource TokenSource { get; set; }
    }
}
