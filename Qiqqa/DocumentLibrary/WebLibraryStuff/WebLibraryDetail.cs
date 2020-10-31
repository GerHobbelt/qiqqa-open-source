﻿using System;
using ProtoBuf;
using Utilities.Strings;

namespace Qiqqa.DocumentLibrary.WebLibraryStuff
{
    [Serializable]
    [ProtoContract]
    public class WebLibraryDetail
    {
        [ProtoMember(1)]
        public string Id { get; set; }
        [ProtoMember(2)]
        public string Title { get; set; }
        [ProtoMember(3)]
        public string Description { get; set; }

        [ProtoMember(4)]
        public bool Deleted { get; set; }
        [ProtoMember(5)]
        public DateTime? LastSynced { get; set; }

        [ProtoMember(6)]
        public string FolderToWatch { get; set; }
        [ProtoMember(7)]
        public bool IsLocalGuestLibrary { get; set; }

        /* Only valid for web libraries */
        [ProtoMember(8)]
        public string ShortWebId { get; set; }
        [ProtoMember(9)]
        public bool IsAdministrator { get; set; }
        [ProtoMember(14)]
        public bool IsReadOnly {
            get;
            set; }
        // Bundles can never sync
        public bool IsReadOnlyLibrary => IsBundleLibrary;

        /* Only valid for intranet libraries */
        [ProtoMember(13)]
        public string IntranetPath { get; set; }
        public bool IsIntranetLibrary => !String.IsNullOrEmpty(IntranetPath);

        /* Only valid for Bundle Libraries */
        [ProtoMember(15)]
        public string BundleManifestJSON { get; set; }
        public bool IsBundleLibrary => !String.IsNullOrEmpty(BundleManifestJSON);

        [ProtoMember(16)]
        public DateTime? LastBundleManifestDownloadTimestampUTC { get; set; }
        [ProtoMember(17)]
        public string LastBundleManifestIgnoreVersion { get; set; }

        [ProtoMember(10)]
        public bool IsPurged { get; set; }

        [ProtoMember(11)]
        public DateTime LastServerSyncNotificationDate { get; set; }
        [ProtoMember(12)]
        public bool AutoSync {
            get;
            set;
        }

        public Library library {
            get;
            set;
        }

        public override string ToString()
        {
            return String.Format("Library {0}: {1}", Id, Title);
        }

        public string DescriptiveTitle
        {
            get
            {
                string s = StringTools.TrimToLengthWithEllipsis(Title);
                if (!String.IsNullOrWhiteSpace(s)) return s;
                s = StringTools.TrimToLengthWithEllipsis(Description);
                if (!String.IsNullOrWhiteSpace(s)) return s;
                return Id;
            }
        }

        public string LibraryType()
        {
            if (IsIntranetLibrary) return "Intranet";
            if (IsBundleLibrary) return "Bundle";
            return "Local";
        }
    }
}
