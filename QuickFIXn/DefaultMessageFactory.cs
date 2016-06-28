using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using QuickFix.Fields;

namespace QuickFix
{
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// The default factory for creating FIX message instances.  (In the v2.0 release, this class should be made sealed.)
    /// </summary>
    public class DefaultMessageFactory : IMessageFactory
    {
        private static readonly Dictionary<string, IMessageFactory> _factories = new Dictionary<string, IMessageFactory>();

        private static readonly string[] MessageAssemblies =
            {
                "QuickFix.FIX.4.0" /* 113Kb */, "QuickFix.FIX.4.1"
                /* 176kb*/, "QuickFix.FIX.4.2" /* 425Kb*/,
                "QuickFix.FIX.4.3" /* 1.1Mb*/, "QuickFix.FIX.4.4"
                /* 3.36Mb*/, "QuickFix.FIX.5.0" /* 5.10Mb*/
            };

        static DefaultMessageFactory()
        {
            //// These are replaced by a dynamic version (reflection)
            ////_factories[FixValues.BeginString.FIX40] = new QuickFix.FIX40.MessageFactory();
            ////_factories[FixValues.BeginString.FIX41] = new QuickFix.FIX41.MessageFactory();
            ////_factories[FixValues.BeginString.FIX42] = new QuickFix.FIX42.MessageFactory();
            ////_factories[FixValues.BeginString.FIX43] = new QuickFix.FIX43.MessageFactory();
            ////_factories[FixValues.BeginString.FIX44] = new QuickFix.FIX44.MessageFactory();
            ////_factories[FixValues.BeginString.FIX50] = new QuickFix.FIX50.MessageFactory();

            // Use reflection to load assemblies and create an instance of each and every available factory
            // Assemblies are loaded from current directory, so you just have to deploy the message file(s) you need
            // It can be a space saver
            // QuickFix.FIX.4.0.dll ~ 113Kb
            // QuickFix.FIX.4.1.dll ~ 176kb
            // QuickFix.FIX.4.2.dll ~ 425Kb
            // QuickFix.FIX.4.3.dll ~ 1.1Mb
            // QuickFix.FIX.4.4.dll ~ 3.36Mb
            // QuickFix.FIX.5.0.dll ~ 5.10Mb
            foreach (var assembly in MessageAssemblies)
            {
                if (!File.Exists(assembly + ".dll"))
                {
                    // This assembly is not in current directory
                    continue;
                }

                try
                {
                    // Load assembly, example "QuickFix.FIX.4.4"
                    var a = Assembly.Load(assembly);

                    // This is the FIX version, for example "FIX.4.4"
                    var fix = assembly.Replace("QuickFix.", string.Empty);

                    // This is the type to instantiate, for example "QuickFix.FIX44.MessageFactory"
                    var type = "QuickFix." + fix.Replace(".", string.Empty) + ".MessageFactory";

                    // Create instance (it should implement IMessageFactory)
                    var messageFactory = a.CreateInstance(type) as IMessageFactory;
                    if (messageFactory != null)
                    {
                        // Add mapping, for example "FIX.4.4" => QuickFix.FIX44.MessageFactory instance
                        _factories[fix] = messageFactory;
                    }
                }
                catch
                {
                    // Didn't find any logging :) probably need Log4Net or something somewhere :)
                }
            }
        }

        public static void Dump(TextWriter writer)
        {
            foreach (var kvp in _factories)
            {
                var fix = kvp.Key;
                var messageFactory = kvp.Value;
                writer.WriteLine($"{fix} => {messageFactory.GetType().FullName} ({messageFactory.GetType().Assembly.Location})");
            }
        }

        #region IMessageFactory Members

        public Message Create(string beginString, string msgType)
        {
            IMessageFactory f = null;

            // FIXME: This is a hack.  FIXT11 could mean 50 or 50sp1 or 50sp2.
            // We need some way to choose which 50 version it is.
            // Choosing 50 here is not adequate.
            if (beginString.Equals(FixValues.BeginString.FIXT11))
            {
                if (!Message.IsAdminMsgType(msgType))
                    f = _factories[FixValues.BeginString.FIX50];
            }
            if(f != null) // really, this should just be in the previous if-block
                return f.Create(beginString, msgType);



            if (!_factories.ContainsKey(beginString))
            {
                Message m = new Message();
                m.Header.SetField(new StringField(QuickFix.Fields.Tags.MsgType, msgType));
                return m;
            }

            f = _factories[beginString];
            return f.Create(beginString, msgType);
        }

        public Group Create(string beginString, string msgType, int groupCounterTag)
        {
            // FIXME: This is a hack.  FIXT11 could mean 50 or 50sp1 or 50sp2.
            // We need some way to choose which 50 version it is.
            // Choosing 50 here is not adequate.
            if(beginString.Equals(FixValues.BeginString.FIXT11))
                return _factories[FixValues.BeginString.FIX50].Create(beginString,msgType,groupCounterTag);

            if (_factories.ContainsKey(beginString))
                return _factories[beginString].Create(beginString, msgType, groupCounterTag);

            throw new UnsupportedVersion(beginString);
        }

        #endregion
    }
}
