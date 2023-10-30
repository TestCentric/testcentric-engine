// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric contributors.
// Licensed under the MIT License. See LICENSE file in root directory.
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using TestCentric.Engine.Internal;
using TestCentric.Engine.Services;
using System.Runtime.Serialization.Formatters;

namespace TestCentric.Engine.Communication.Transports.Remoting
{
    /// <summary>
    /// Summary description for TestAgencyRemotingTransport.
    /// </summary>
    public class TestAgencyRemotingTransport : MarshalByRefObject, ITestAgencyTransport, ITestAgency, IDisposable
    {
        private static readonly Logger log = InternalTrace.GetLogger(typeof(TestAgencyRemotingTransport));

        private ITestAgency _agency;
        private string _uri;
        private int _port;

        private TcpChannel _channel;
        private bool _isMarshalled;

        private object _theLock = new object();

        public TestAgencyRemotingTransport(ITestAgency agency, string uri, int port)
        {
            Guard.ArgumentNotNull(agency, nameof(agency));
            Guard.ArgumentNotNullOrEmpty(uri, nameof(uri));

            _agency = agency;
            _uri = uri;
            _port = port;
        }

        public string ServerUrl => string.Format("tcp://127.0.0.1:{0}/{1}", _port, _uri);

        public bool Start()
        {
            lock (_theLock)
            {
                _channel = GetTcpChannel(_uri + "Channel", _port);

                RemotingServices.Marshal(this, _uri);
                _isMarshalled = true;
            }

            if (_port == 0)
            {
                ChannelDataStore store = this._channel.ChannelData as ChannelDataStore;
                if (store != null)
                {
                    string channelUri = store.ChannelUris[0];
                    _port = int.Parse(channelUri.Substring(channelUri.LastIndexOf(':') + 1));
                }
            }

            return true;
        }

        private static TcpChannel GetTcpChannel(string name, int port)
        {
            var existingChannel = ChannelServices.GetChannel(name) as TcpChannel;
            if (existingChannel != null) return existingChannel;

            // NOTE: Retries are normally only needed when rapidly creating
            // and destroying channels, as in running the NUnit tests.
            for (var retries = 0; retries < 10; retries++)
                try
                {
                    var newChannel = CreateTcpChannel(name, port);
                    ChannelServices.RegisterChannel(newChannel, false);
                    return newChannel;
                }
                catch (Exception ex)
                {
                    log.Error("Failed to create/register channel." + Environment.NewLine + ExceptionHelper.BuildMessageAndStackTrace(ex));
                    Thread.Sleep(300);
                }

            return null;
        }

        private static TcpChannel CreateTcpChannel(string name, int port)
        {
            var props = new Dictionary<string, object>
            {
                { "port", port },
                { "name", name },
                { "bindTo", "127.0.0.1" }
            };

            var serverProvider = new BinaryServerFormatterSinkProvider {
                TypeFilterLevel = TypeFilterLevel.Full
            };

            var clientProvider = new BinaryClientFormatterSinkProvider();

            return new TcpChannel(
                props,
                clientProvider,
                (IServerChannelSinkProvider)serverProvider);
        }

        [System.Runtime.Remoting.Messaging.OneWay]
        public void Stop()
        {
            lock( _theLock )
            {
                if ( this._isMarshalled )
                {
                    RemotingServices.Disconnect( this );
                    this._isMarshalled = false;
                }

                if ( this._channel != null )
                {
                    try
                    {
                        ChannelServices.UnregisterChannel(this._channel);
                        this._channel = null;
                    }
                    catch (RemotingException)
                    {
                        // Mono 4.4 appears to unregister the channel itself
                        // so don't do anything here.
                    }
                }

                Monitor.PulseAll( _theLock );
            }
        }

        public void Register(ITestAgent agent)
        {
            _agency.Register(agent);
        }

        //public void WaitForStop()
        //{
        //    lock( _theLock )
        //    {
        //        Monitor.Wait( _theLock );
        //    }
        //}

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    Stop();

                _disposed = true;
            }
        }

        /// <summary>
        /// Overridden to cause object to live indefinitely
        /// </summary>
        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
