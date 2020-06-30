using System.Threading;

namespace Lucene.Net.Store.Azure
{
    public static class BlobMutexManager
    {
        public static Mutex GrabMutex(string name)
        {
            var mutexName = "luceneSegmentMutex_" + name;
            if (Mutex.TryOpenExisting(mutexName, out var mutex))
            {
                return mutex;
            }
            else
            {
                return new Mutex(false, mutexName, out _);
            }
        }
    }
}
