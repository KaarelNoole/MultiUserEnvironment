using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VideoOS.Platform.Messaging;
using VideoOS.Platform;

namespace MultiUserEnvironment
{
    public class ConfigurationMonitor : IDisposable
    {
        private MessageCommunication _messageCommunication;
        private object _obj;
        private Timer _reloadTimer;
        private bool _firstTime = true;
        private ServerId _serverId;

        private List<UserContext> _userContexts = new List<UserContext>();
        private Dictionary<UserContext, HashSet<FQID>> _recordersToReload = new Dictionary<UserContext, HashSet<FQID>>();

        public ConfigurationMonitor(ServerId serverId)
        {
            _reloadTimer = new Timer(ReloadConfigTimerHandler);
            _serverId = serverId;

            MessageCommunicationManager.Start(_serverId);
            _messageCommunication = MessageCommunicationManager.Get(_serverId);

            _obj = _messageCommunication.RegisterCommunicationFilter(SystemConfigurationChangedIndicationHandler,
                new CommunicationIdFilter(MessageId.System.SystemConfigurationChangedIndication));

            _messageCommunication.ConnectionStateChangedEvent += new EventHandler(_messageCommunication_ConnectionStateChangedEvent);

            _reloadTimer.Change(0, 15000);

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_reloadTimer != null)
                {
                    _reloadTimer.Dispose();
                    _reloadTimer = null;

                    _messageCommunication.UnRegisterCommunicationFilter(_obj);
                    MessageCommunicationManager.Stop(_serverId);
                }
            }
        }


        public delegate void ShowMessageDelegate(String message);
        public event ShowMessageDelegate ShowMessage = delegate { };

        public delegate void ConfigurationNowReloadedDelegate();
        public event ConfigurationNowReloadedDelegate ConfigurationNowReloaded = delegate { };

        public delegate void ConnectionStateChangedDelegate();
        public event ConnectionStateChangedDelegate ConnectionStateChanged = delegate { };

        public bool IsConnectedToEventServer = false;

        public void AddUserContext(UserContext userContext)
        {
            lock (_userContexts)
            {
                _userContexts.Add(userContext);
            }
        }

        public void RemoveUserContext(UserContext userContext)
        {
            lock (_userContexts)
            {
                if (_recordersToReload.ContainsKey(userContext))
                    _recordersToReload.Remove(userContext);
                _userContexts.Remove(userContext);
            }
        }
        void _messageCommunication_ConnectionStateChangedEvent(object sender, EventArgs e)
        {
            IsConnectedToEventServer = _messageCommunication.IsConnected;
            ConnectionStateChanged();
        }

        private object SystemConfigurationChangedIndicationHandler(Message message, FQID dest, FQID sender)
        {
            List<FQID> fqidList = message.Data as List<FQID>;
            if (fqidList != null)
            {
                lock (_userContexts)
                {
                    foreach (FQID fqid in fqidList)
                    {
                        if (fqid.ServerId.ServerType != "XP")
                        {
                            foreach (UserContext userContext in _userContexts)
                            {
                                Item item = userContext.Configuration.GetItem(fqid.ObjectId, fqid.Kind);
                                if (item == null && fqid.Kind == Kind.Server)
                                {
                                    
                                    item = userContext.Configuration.GetItem(fqid.ParentId, fqid.Kind);
                                }
                                FQID serverFQID = null;
                                if (fqid.Kind == Kind.Server)
                                    serverFQID = fqid;
                                else
                                {
                                    Item recorderItem = Configuration.Instance.GetItem(fqid.ServerId.Id, Kind.Server);
                                    if (recorderItem != null)
                                        serverFQID = recorderItem.FQID;
                                }
                                if (serverFQID != null)
                                {
                                    if (!_recordersToReload.ContainsKey(userContext))
                                        _recordersToReload.Add(userContext, new HashSet<FQID>());
                                    _recordersToReload[userContext].Add(serverFQID);
                                }
                            }
                        }
                    }

                    _reloadTimer.Change(0, 15000);

                }
                ShowMessage("--- Event received to load new configuration");
            }
            return null;
        }

        private void ReloadConfigTimerHandler(object state)
        {
            _reloadTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (!_firstTime)
            {
                lock (_userContexts)
                {
                    foreach (UserContext userContext in _recordersToReload.Keys)
                    {
                        foreach (FQID fqid in _recordersToReload[userContext])
                        {
                            VideoOS.Platform.SDK.MultiUserEnvironment.ReloadConfiguration(userContext, fqid);
                        }
                    }
                    _recordersToReload.Clear();
                }
            }
            _firstTime = false;

            ShowMessage("--- configuration reloaded");

            ConfigurationNowReloaded();
        }
    }
}