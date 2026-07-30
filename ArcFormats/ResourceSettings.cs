//! \file       ResourceSettings.cs
//! \date       2018 Jan 08
//! \brief      Persistent resource settings implementation.
//

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text;
using GameRes.Formats.Strings;

namespace GameRes.Formats
{
    internal class LocalResourceSetting : ResourceSettingBase
    {
#if NET6_0_OR_GREATER
        static readonly Dictionary<string, object> s_values = new Dictionary<string, object>();

        public override object Value {
            get {
                object value;
                return s_values.TryGetValue (Name, out value) ? value : null;
            }
            set { s_values[Name] = value; }
        }
#else
        public override object Value {
            get { return Properties.Settings.Default[Name]; }
            set { Properties.Settings.Default[Name] = value; }
        }
#endif

        public LocalResourceSetting () { }

        public LocalResourceSetting (string name) : this (name, name) { }

        public LocalResourceSetting (string name, string text)
        {
            Name = name;
            Text = arcStrings.ResourceManager.GetString (text, arcStrings.Culture) ?? text;
        }
    }

    internal class EncodingSetting : LocalResourceSetting
    {
        static readonly Encoding DefaultEncoding = Encodings.cp932;

        public override object Value {
            get {
                try
                {
                    return Encoding.GetEncoding ((int)base.Value);
                }
                catch // fallback to CP932
                {
                    Trace.WriteLine (string.Format ("Unknown encoding code page {0}", base.Value));
                    return DefaultEncoding;
                }
            }
            set { base.Value = ((Encoding)value).CodePage; }
        }

        public EncodingSetting () { }

        public EncodingSetting (string name) : base (name) { }

        public EncodingSetting (string name, string text) : base (name, text) { }
    }

    [Export(typeof(ISettingsManager))]
    internal class SettingsManager : ISettingsManager
    {
        public void UpgradeSettings ()
        {
#if !NET6_0_OR_GREATER
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }
#endif
        }

        public void SaveSettings ()
        {
#if !NET6_0_OR_GREATER
            Properties.Settings.Default.Save();
#endif
        }
    }
}
