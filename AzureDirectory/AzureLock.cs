using Azure;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace Lucene.Net.Store.Azure
{
    /// <summary>
    /// Implements lock semantics on AzureDirectory via a blob lease
    /// </summary>
    public class AzureLock : Lock
    {
        private string _lockFile;
        private AzureDirectory _azureDirectory;
        private string _leaseid;

        public AzureLock(string lockFile, AzureDirectory directory)
        {
            _lockFile = lockFile;
            _azureDirectory = directory;
        }

        #region Lock methods
        override public bool IsLocked()
        {
            var blob = _azureDirectory.BlobContainer.GetBlockBlobClient(_lockFile);

            try
            {
                if (!String.IsNullOrEmpty(_leaseid))
                {
                    Debug.Print("IsLocked() : {0}", _leaseid);
                    return true;
                }

                var leaseClient = blob.GetBlobLeaseClient();
                var tempLease = leaseClient.Acquire(TimeSpan.FromSeconds(60));
                leaseClient.Release();
                return false;
            }
            catch (RequestFailedException ex)
            when (ex.Status == (int)HttpStatusCode.Conflict && ex.ErrorCode == "LeaseAlreadyPresent")
            {
                Debug.Print("IsLocked() : TRUE");
                return true;
            }
            catch (RequestFailedException ex)
            {
                if (_handleWebException(blob, ex))
                    return IsLocked();
            }
            _leaseid = null;
            return false;
        }

        public override bool Obtain()
        {
            var blob = _azureDirectory.BlobContainer.GetBlockBlobClient(_lockFile);
            try
            {
                Debug.Print("AzureLock:Obtain({0}) : {1}", _lockFile, _leaseid);
                if (String.IsNullOrEmpty(_leaseid))
                {
                    var leaseClient = blob.GetBlobLeaseClient();
                    leaseClient.Acquire(TimeSpan.FromSeconds(60));
                    _leaseid = leaseClient.LeaseId;
                    Debug.Print("AzureLock:Obtain({0}): AcquireLease : {1}", _lockFile, _leaseid);

                    // keep the lease alive by renewing every 30 seconds
                    long interval = (long)TimeSpan.FromSeconds(30).TotalMilliseconds;
                    _renewTimer = new Timer((obj) =>
                        {
                            try
                            {
                                AzureLock al = (AzureLock)obj;
                                al.Renew();
                            }
                            catch (Exception err) { Debug.Print(err.ToString()); }
                        }, this, interval, interval);
                }
                return !String.IsNullOrEmpty(_leaseid);
            }
            catch (RequestFailedException webErr)
            {
                if (_handleWebException(blob, webErr))
                    return Obtain();
            }
            return false;
        }

        private Timer _renewTimer;

        public void Renew()
        {
            if (!String.IsNullOrEmpty(_leaseid))
            {
                Debug.Print("AzureLock:Renew({0} : {1}", _lockFile, _leaseid);
                var blob = _azureDirectory.BlobContainer.GetBlockBlobClient(_lockFile);
                var leaseClient = blob.GetBlobLeaseClient(_leaseid);
                leaseClient.Renew();
            }
        }

        public override void Release()
        {
            Debug.Print("AzureLock:Release({0}) {1}", _lockFile, _leaseid);
            if (!String.IsNullOrEmpty(_leaseid))
            {
                var blob = _azureDirectory.BlobContainer.GetBlockBlobClient(_lockFile);
                var leaseClient = blob.GetBlobLeaseClient(_leaseid);
                leaseClient.Release();
                if (_renewTimer != null)
                {
                    _renewTimer.Dispose();
                    _renewTimer = null;
                }
                _leaseid = null;
            }
        }
        #endregion

        public void BreakLock()
        {
            Debug.Print("AzureLock:BreakLock({0}) {1}", _lockFile, _leaseid);
            var blob = _azureDirectory.BlobContainer.GetBlockBlobClient(_lockFile);
            try
            {
                blob.GetBlobLeaseClient().Break();
            }
            catch (Exception)
            {
            }
            _leaseid = null;
        }

        public override System.String ToString()
        {
            return String.Format("AzureLock@{0}.{1}", _lockFile, _leaseid);
        }

        private bool _handleWebException(BlockBlobClient blob, RequestFailedException err)
        {
            if (err.Status == 404 || err.Status == 409)
            {
                _azureDirectory.CreateContainer();
                using (var stream = new MemoryStream())
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(_lockFile);
                    writer.Flush();
                    stream.Position = 0;
                    blob.Upload(stream);
                }
                return true;
            }
            return false;
        }

    }

}
