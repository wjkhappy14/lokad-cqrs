﻿#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 

// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence

#endregion

using System;
using System.Linq;
using System.Transactions;
using Lokad.Cqrs.Core.Envelope;

namespace Lokad.Cqrs.Core.Outbox
{
    sealed class DefaultMessageSender : IMessageSender
    {
        readonly IQueueWriter _queue;
        readonly ISystemObserver _observer;
        readonly Func<string> _idGenerator;

        public DefaultMessageSender(IQueueWriter queue, ISystemObserver observer, Func<string> idGenerator)
        {
            _queue = queue;
            _observer = observer;
            _idGenerator = idGenerator;
        }

        public void SendOne(object content)
        {
            InnerSendBatch(cb => { }, new[] {content});
        }

        public void SendOne(object content, Action<EnvelopeBuilder> configure)
        {
            InnerSendBatch(configure, new[] {content});
        }


        public void SendBatch(object[] content)
        {
            InnerSendBatch(cb => { }, content);
        }

        public void SendBatch(object[] content, Action<EnvelopeBuilder> builder)
        {
            InnerSendBatch(builder, content);
        }



        void InnerSendBatch(Action<EnvelopeBuilder> configure, object[] messageItems) {
            if (messageItems.Length == 0)
                return;

            var id = _idGenerator();

            var builder = new EnvelopeBuilder(id);
            foreach (var item in messageItems)
            {
                builder.AddItem(item);
            }
            
            configure(builder);
            var envelope = builder.Build();

            if (Transaction.Current == null)
            {
                _queue.PutMessage(envelope);
                _observer.Notify(new EnvelopeSent(_queue.Name, envelope.EnvelopeId, false,
                    envelope.Items.Select(x => x.MappedType.Name).ToArray()));
            }
            else
            {
                var action = new CommitActionEnlistment(() =>
                    {
                        _queue.PutMessage(envelope);
                        _observer.Notify(new EnvelopeSent(_queue.Name, envelope.EnvelopeId, true,
                            envelope.Items.Select(x => x.MappedType.Name).ToArray()));
                    });
                Transaction.Current.EnlistVolatile(action, EnlistmentOptions.None);
            }
        }
    }
}