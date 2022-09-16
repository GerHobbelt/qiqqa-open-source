﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using Newtonsoft.Json;
using Qiqqa.Common.TagManagement;
using Qiqqa.DocumentLibrary;
using Qiqqa.DocumentLibrary.WebLibraryStuff;
using Qiqqa.Documents.Common;
using Qiqqa.Documents.PDF.CitationManagerStuff;
using Qiqqa.Documents.PDF.DiskSerialisation;
using Qiqqa.Documents.PDF.PDFControls.Page.Tools;
using Qiqqa.Documents.PDF.PDFRendering;
using Qiqqa.UtilisationTracking;
using Utilities;
using Utilities.BibTex;
using Utilities.BibTex.Parsing;
using Utilities.Files;
using Utilities.GUI;
using Utilities.Misc;
using Utilities.PDF.MuPDF;
using Utilities.Reflection;
using Utilities.Strings;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;


namespace Qiqqa.Documents.PDF
{
    /// <summary>
    /// ******************* NB NB NB NB NB NB NB NB NB NB NB ********************************
    ///
    /// ALL PROPERTIES STORED IN THE DICTIONARY MUST BE SIMPLE TYPES - string, int or double.
    /// NO DATES, NO COLORS, NO STRUCTs.
    /// If you want to store Color and DateTime, then there are helper methods on the DictionaryBasedObject to convert TO/FROM.  Use those!
    /// Otherwise platform independent serialisation will break!
    ///
    /// ******************* NB NB NB NB NB NB NB NB NB NB NB ********************************
    /// </summary>

    public partial class PDFDocument
    {
        private TypedWeakReference<WebLibraryDetail> library;
        public WebLibraryDetail LibraryRef => library?.TypedTarget;

        private DictionaryBasedObject dictionary = new DictionaryBasedObject();

        internal bool dirtyNeedsReindexing = false;

        public string GetAttributesAsJSON()
        {
            string json = JsonConvert.SerializeObject(dictionary.Attributes, Formatting.Indented);
            return json;
        }

        internal static readonly PropertyDependencies property_dependencies = new PropertyDependencies();

        static PDFDocument()
        {
            PDFDocument p = null;

            property_dependencies.Add(() => p.TitleCombined, () => p.Title);
            property_dependencies.Add(() => p.TitleCombined, () => p.BibTex);
            property_dependencies.Add(() => p.AuthorsCombined, () => p.Authors);
            property_dependencies.Add(() => p.AuthorsCombined, () => p.BibTex);
            property_dependencies.Add(() => p.YearCombined, () => p.Year);
            property_dependencies.Add(() => p.YearCombined, () => p.BibTex);

            property_dependencies.Add(() => p.Publication, () => p.BibTex);
            property_dependencies.Add(() => p.BibTex, () => p.Publication);

            property_dependencies.Add(() => p.Title, () => p.TitleCombined);
            property_dependencies.Add(() => p.Title, () => p.TitleCombinedReason);
            property_dependencies.Add(() => p.BibTex, () => p.TitleCombined);
            property_dependencies.Add(() => p.BibTex, () => p.TitleCombinedReason);
            property_dependencies.Add(() => p.TitleSuggested, () => p.TitleCombined);
            property_dependencies.Add(() => p.TitleSuggested, () => p.TitleCombinedReason);
            property_dependencies.Add(() => p.DownloadLocation, () => p.TitleCombined);
            property_dependencies.Add(() => p.DownloadLocation, () => p.TitleCombinedReason);
            property_dependencies.Add(() => p.TitleCombined, () => p.TitleCombinedReason);

            property_dependencies.Add(() => p.Authors, () => p.AuthorsCombined);
            property_dependencies.Add(() => p.Authors, () => p.AuthorsCombinedReason);
            property_dependencies.Add(() => p.BibTex, () => p.AuthorsCombined);
            property_dependencies.Add(() => p.BibTex, () => p.AuthorsCombinedReason);
            property_dependencies.Add(() => p.AuthorsSuggested, () => p.AuthorsCombined);
            property_dependencies.Add(() => p.AuthorsSuggested, () => p.AuthorsCombinedReason);
            property_dependencies.Add(() => p.AuthorsCombined, () => p.AuthorsCombinedReason);

            property_dependencies.Add(() => p.Year, () => p.YearCombined);
            property_dependencies.Add(() => p.Year, () => p.YearCombinedReason);
            property_dependencies.Add(() => p.BibTex, () => p.YearCombined);
            property_dependencies.Add(() => p.BibTex, () => p.YearCombinedReason);
            property_dependencies.Add(() => p.YearSuggested, () => p.YearCombined);
            property_dependencies.Add(() => p.YearSuggested, () => p.YearCombinedReason);
            property_dependencies.Add(() => p.YearCombined, () => p.YearCombinedReason);

            property_dependencies.Add(() => p.BibTex, () => p.Publication);
            property_dependencies.Add(() => p.BibTex, () => p.Id);
            property_dependencies.Add(() => p.BibTex, () => p.Abstract);
        }

        internal PDFDocument(WebLibraryDetail web_library_detail)
        {
            this.library = new TypedWeakReference<WebLibraryDetail>(web_library_detail);
            dictionary = new DictionaryBasedObject();
        }

        internal PDFDocument(WebLibraryDetail web_library_detail, DictionaryBasedObject dictionary, PDFAnnotationList prefetched_annotations_for_document = null)
        {
            this.library = new TypedWeakReference<WebLibraryDetail>(web_library_detail);
            this.dictionary = dictionary;

            // process any prefetched annotations that we may have as usual:
            if (prefetched_annotations_for_document != null)
            {
                annotations = prefetched_annotations_for_document;
                lock (access_lock)
                {
                    dirtyNeedsReindexing = (prefetched_annotations_for_document.Count > 0);
                }
            }
        }

        public string PageCountAsString
        {
            get
            {
                int n = PageCount;
                if (n < 0)
                {
                    string rv = PDFErrors.ToString(n);
                    if (rv != null)
                    {
                        return rv;
                    }
                    return $"<ERROR { n }>";
                }
                if (n == 0)
                {
                    if (IsVanillaReference) return "<vanilla ref>";
                    if (IsCorruptedDocument) return "<corrupted PDF>";
                    return "<empty document>";
                }
                return n.ToString();
            }
        }

        public string Fingerprint
        {
            get
            {
                string rv = dictionary["Fingerprint"] as string;
                if (String.IsNullOrEmpty(rv))
                {
                    ASSERT.Test(false, "Should never get here!");
                    WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

                    rv = StreamFingerprint.FromFile(DocumentPath);
                    dictionary["Fingerprint"] = rv;
                }
                return rv;
            }
            /* protected */
            set => dictionary["Fingerprint"] = value;
        }

        public string PDFPassword => LibraryRef.Xlibrary.PasswordManager.GetPassword(this);

        /// <summary>
        /// Unique id for both this document and the library that it exists in.
        /// </summary>
        public string UniqueId => string.Format("{0}_{1}", Fingerprint, LibraryRef.Id);

        public string FileType
        {
            get => dictionary["FileType"] as string;
            set => dictionary["FileType"] = value.ToLower();
        }

        private BibTexItem bibtex_item = null;
        private bool bibtex_item_parsed = false;
        public BibTexItem BibTexItem
        {
            get
            {
                if (!bibtex_item_parsed)
                {
                    bibtex_item = BibTexParser.ParseOne(BibTex, true);
                    bibtex_item_parsed = true;

                    if (null != bibtex_item)
                    {
                        // if the BibTeX is ill formatted, make sure some basic sanity is provided:
                        if (String.IsNullOrWhiteSpace(BibTexItem.Type))
                        {
                            BibTexItem.Type = "empty_and_erroneous";
                        }
                        if (String.IsNullOrWhiteSpace(BibTexItem.Key))
                        {
                            BibTexItem.Key = BibTexTools.GenerateRandomBibTeXKey();
                        }
                    }
                }

                return bibtex_item;
            }
        }

        public string BibTex
        {
            get => dictionary["BibTex"] as string;
            set
            {
                // Clear the cached item
                bibtex_item = null;
                bibtex_item_parsed = false;

                // Store the new value
                dictionary["BibTex"] = value;

                // If the BibTeX contains title, author or year, use those by clearing out any overriding values
                if (!String.IsNullOrEmpty(BibTexTools.GetTitle(BibTexItem)))
                {
                    Title = null;
                }
                if (!String.IsNullOrEmpty(BibTexTools.GetAuthor(BibTexItem)))
                {
                    Authors = null;
                }
                if (!String.IsNullOrEmpty(BibTexTools.GetYear(BibTexItem)))
                {
                    Year = null;
                }
            }
        }

        public string BibTexKey
        {
            get
            {
                try
                {
                    BibTexItem item = BibTexItem;
                    if (null != item)
                    {
                        return item.Key;
                    }
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "Exception in BibTexKey");
                }

                return null;
            }
        }

        public string Title
        {
            get => dictionary["Title"] as string;
            set => dictionary["Title"] = value;
        }
        public string TitleSuggested
        {
            get => dictionary["TitleSuggested"] as string;
            set => dictionary["TitleSuggested"] = value;
        }
        public string TitleCombinedReason => String.Format(
                    "Final decision: {0}\n\nYour override: {1}\nBibTeX: {2}\nSuggested: {3}\nSource: {4}",
                    TitleCombined,
                    Title,
                    BibTexTools.GetTitle(BibTexItem),
                    TitleSuggested,
                    DownloadLocation
                    );

        public string TitleCombined
        {
            get
            {
                if (!String.IsNullOrEmpty(Title)) return Title;

                string bibtex = BibTexTools.GetTitle(BibTexItem);
                if (!String.IsNullOrEmpty(bibtex)) return bibtex;

                if (!String.IsNullOrEmpty(TitleSuggested)) return TitleSuggested;

                if (!String.IsNullOrEmpty(DownloadLocation)) return DownloadLocation;

                return Constants.TITLE_UNKNOWN;
            }
            set
            {
                string old_combined = TitleCombined;
                if (null != old_combined && 0 == old_combined.CompareTo(value))
                {
                    return;
                }

                // If they are clearing out a value, clear the title
                if (String.IsNullOrEmpty(value))
                {
                    Title = null;
                    return;
                }

                // Then see if they are updating bibtex
                if (String.IsNullOrEmpty(Title) && !String.IsNullOrEmpty(BibTexTools.GetTitle(BibTexItem)))
                {
                    BibTex = BibTexTools.SetTitle(BibTex, value);
                    return;
                }

                // If we get here, they are changing the Title cos there is no bibtex to update...
                Title = value;
            }
        }

        /// <summary>
        /// Is true if the user made this title by hand (e.g. typed it in or got some BibTeX)
        /// </summary>
        public bool IsTitleGeneratedByUser
        {
            get
            {
                if (!String.IsNullOrEmpty(Title)) return true;
                string bibtex = BibTexTools.GetTitle(BibTexItem);
                if (!String.IsNullOrEmpty(bibtex)) return true;

                return false;
            }
        }

        public string Authors
        {
            get => dictionary["Authors"] as string;
            set => dictionary["Authors"] = value;
        }
        public string AuthorsSuggested
        {
            get => dictionary["AuthorsSuggested"] as string;
            set => dictionary["AuthorsSuggested"] = value;
        }
        public string AuthorsCombinedReason => String.Format(
                    "Final decision: {0}\n\nYour override: {1}\nBibTeX: {2}\nSuggested: {3}",
                    AuthorsCombined,
                    Authors,
                    BibTexTools.GetAuthor(BibTexItem),
                    AuthorsSuggested
                    );

        public string AuthorsCombined
        {
            get
            {
                if (!String.IsNullOrEmpty(Authors)) return Authors;

                string bibtex = BibTexTools.GetAuthor(BibTexItem);
                if (!String.IsNullOrEmpty(bibtex)) return bibtex;

                if (!String.IsNullOrEmpty(AuthorsSuggested)) return AuthorsSuggested;

                return Constants.UNKNOWN_AUTHORS;
            }
            set
            {
                string old_combined = AuthorsCombined;
                if (null != old_combined && 0 == old_combined.CompareTo(value))
                {
                    return;
                }

                // If they are clearing out a value, clear the authors override
                if (String.IsNullOrEmpty(value))
                {
                    Authors = null;
                    return;
                }

                // Then see if they are updating bibtex
                if (String.IsNullOrEmpty(Authors) && !String.IsNullOrEmpty(BibTexTools.GetAuthor(BibTexItem)))
                {
                    BibTex = BibTexTools.SetAuthor(BibTex, value);
                    return;
                }

                // If we get here, they are changing the Authors cos there is no bibtex to update...
                Authors = value;
            }
        }

        public string Year
        {
            get => dictionary["Year"] as string;
            set => dictionary["Year"] = value;
        }
        public string YearSuggested
        {
            get => dictionary["YearSuggested"] as string;
            set => dictionary["YearSuggested"] = value;
        }
        public string YearCombinedReason => String.Format(
                    "Final decision: {0}\n\nYour override: {1}\nBibTeX: {2}\nSuggested: {3}",
                    YearCombined,
                    Year,
                    BibTexTools.GetYear(BibTexItem),
                    YearSuggested
                    );

        /// <summary>
        /// Produce the document's year of publication.
        ///
        /// When producing (getting) this value, the priority is:
        ///
        /// - check the BibTeX `year` field and return that one when it's non-empty
        /// - check the manual-entry `year` field (@xref Year)
        /// - check the suggested year field (@xref YearSuggested)
        /// - if also else fails, return the UNKNOWN_YEAR value.
        ///
        /// When setting this value, the first action in this priority list is executed, where the conditions pass:
        ///
        /// - check if there's a non-empty (partial) BibTeX record: when there is, add/update the `year` field
        /// - update the manual-entry Year field (@xref Year)
        /// </summary>
        public string YearCombined
        {
            get
            {
                if (!String.IsNullOrEmpty(Year)) return Year;

                string bibtex_year = BibTexTools.GetYear(BibTexItem);
                if (!String.IsNullOrEmpty(bibtex_year)) return bibtex_year;

                if (!String.IsNullOrEmpty(YearSuggested)) return YearSuggested;

                return Constants.UNKNOWN_YEAR;
            }
            set
            {
                string old_combined = YearCombined;
                if (null != old_combined && 0 == old_combined.CompareTo(value))
                {
                    return;
                }

                // If they are clearing out a value, clear the title
                if (String.IsNullOrEmpty(value))
                {
                    Year = null;
                    return;
                }

                // Then see if they are updating bibtex
                if (String.IsNullOrEmpty(Year) && !String.IsNullOrEmpty(BibTexTools.GetYear(BibTexItem)))
                {
                    BibTex = BibTexTools.SetYear(BibTex, value);
                    return;
                }

                // If we get here, they are changing the Year cos there is no bibtex to update...
                Year = value;
            }
        }

        public string Publication
        {
            get => BibTexTools.GetGenericPublication(BibTexItem);

            set
            {
                BibTexItem bibtex_item = BibTexItem;
                if (null != bibtex_item)
                {
                    BibTexTools.SetGenericPublication(bibtex_item, value);
                    BibTex = bibtex_item.ToBibTex();
                }
            }
        }

        public string Id
        {
            get
            {
                BibTexItem bibtex_item = BibTexItem;
                if (null != bibtex_item)
                {
                    return bibtex_item.Key;
                }
                else
                {
                    return "";
                }
            }
        }

        public string DownloadLocation
        {
            get => dictionary["DownloadLocation"] as string;
            set => dictionary["DownloadLocation"] = value;
        }

        private DateTime? date_added_to_db = null;
        public DateTime? DateAddedToDatabase
        {
            get
            {
                if (date_added_to_db.HasValue) return date_added_to_db.Value;

                date_added_to_db = dictionary.GetDateTime("DateAddedToDatabase");
                return date_added_to_db;
            }
            set
            {
                date_added_to_db = null;
                dictionary.SetDateTime("DateAddedToDatabase", value);
            }
        }

        private DateTime? date_last_modified = null;
        public DateTime? DateLastModified
        {
            get
            {
                if (date_last_modified.HasValue) return date_last_modified.Value;

                date_last_modified = dictionary.GetDateTime("DateLastModified");
                return date_last_modified;
            }
            set
            {
                date_last_modified = null;
                dictionary.SetDateTime("DateLastModified", value);
            }
        }

        private DateTime? date_last_read = null;
        public DateTime? DateLastRead
        {
            get
            {
                if (date_last_read.HasValue) return date_last_read.Value;

                date_last_read = dictionary.GetDateTime("DateLastRead");
                return date_last_read;
            }
            set
            {
                date_last_read = null;
                dictionary.SetDateTime("DateLastRead", value);
            }
        }

        public DateTime? DateLastCited
        {
            get => dictionary.GetDateTime("DateLastCited");
            set => dictionary.SetDateTime("DateLastCited", value);
        }

        public void MarkAsModified()
        {
            DateLastModified = DateTime.UtcNow;
        }

        public string ReadingStage
        {
            get => dictionary["ReadingStage"] as string;
            set => dictionary["ReadingStage"] = value as string;
        }

        public bool? HaveHardcopy
        {
            get => dictionary["HaveHardcopy"] as bool?;
            set => dictionary["HaveHardcopy"] = value as bool?;
        }

        public bool? IsFavourite
        {
            get => dictionary["IsFavourite"] as bool?;
            set => dictionary["IsFavourite"] = value as bool?;
        }

        public string Rating
        {
            get => dictionary["Rating"] as string;
            set => dictionary["Rating"] = value as string;
        }

        public string Comments
        {
            get => dictionary["Comments"] as string;
            set => dictionary["Comments"] = value as string;
        }

        public string Abstract
        {
            get
            {
                // First check if there is an abstract override
                {
                    string abstract_override = dictionary["AbstractOverride"] as string;
                    if (!String.IsNullOrEmpty(abstract_override))
                    {
                        return abstract_override;
                    }
                }

                // Then check if there is an abstract in the bibtex
                {
                    BibTexItem item = BibTexItem;
                    if (null != item)
                    {
                        string abstract_bibtex = item["abstract"];
                        if (!String.IsNullOrEmpty(abstract_bibtex))
                        {
                            return abstract_bibtex;
                        }
                    }
                }

                // Otherwise try get the abstract from the doc itself
                return PDFAbstractExtraction.GetAbstractForDocument(this);
            }
            set => dictionary["AbstractOverride"] = value as string;
        }

        public string Bookmarks
        {
            get => dictionary["Bookmarks"] as string;
            set => dictionary["Bookmarks"] = value as string;
        }

        public Color Color
        {
            get => dictionary.GetColor("ColorWrapper");
            set => dictionary.SetColor("ColorWrapper", value);
        }

        public int PageLastRead
        {
            get
            {
                int value = Convert.ToInt32(dictionary["PageLastRead"] ?? 0);
                int pageCount = PageCount;
                if (value < 0 || value > pageCount)
                {
                    Logging.Error($"Reading an invalid PageLastRead value { dictionary["PageLastRead"] } from the database, while the total page count is { PageCountAsString }");
                    value = Math.Max(0, Math.Min(pageCount, value));
                }
                return value;
            }
            set
            {
                int pageCount = PageCount;
                if (value < 0 || value > pageCount)
                {
                    Logging.Error($"Setting an invalid PageLastRead value { value }, while the total page count is { PageCountAsString }");
                    value = Math.Max(0, Math.Min(pageCount, value));
                }
                dictionary["PageLastRead"] = value;
            }
        }

        public bool Deleted
        {
            get => (bool)(dictionary["Deleted"] ?? false);
            set => dictionary["Deleted"] = value;
        }

        // --- AutoSuggested ------------------------------------------------------------------------------

        public bool AutoSuggested_PDFMetadata
        {
            get => (dictionary["AutoSuggested_PDFMetadata"] as bool?) ?? false;
            set => dictionary["AutoSuggested_PDFMetadata"] = value;
        }

        public bool AutoSuggested_OCRFrontPage
        {
            get => (dictionary["AutoSuggested_OCRFrontPage"] as bool?) ?? false;
            set => dictionary["AutoSuggested_OCRFrontPage"] = value;
        }

        public bool AutoSuggested_BibTeXSearch
        {
            get => (dictionary["AutoSuggested_BibTeXSearch"] as bool?) ?? false;
            set => dictionary["AutoSuggested_BibTeXSearch"] = value;
        }

        //

        // --- Tags ------------------------------------------------------------------------------

        public void AddTag(string new_tag_bundle)
        {
            bool notify;
            lock (access_lock)
            {
                notify = __AddTag(new_tag_bundle);
            }

            if (notify)
            {
                Bindable.NotifyPropertyChanged(nameof(Tags));
                TagManager.Instance.ProcessDocument(this);
            }
        }

        public void RemoveTag(string dead_tag_bundle)
        {
            bool notify;

            lock (access_lock)
            {
                notify = __RemoveTag(dead_tag_bundle);
            }

            if (notify)
            {
                Bindable.NotifyPropertyChanged(nameof(Tags));
                TagManager.Instance.ProcessDocument(this);
            }
        }

        private bool __AddTag(string new_tag_bundle)
        {
            HashSet<string> new_tags = TagTools.ConvertTagBundleToTags(new_tag_bundle);

            HashSet<string> tags = TagTools.ConvertTagBundleToTags(Tags);
            int tag_count_old = tags.Count;
            tags.UnionWith(new_tags);
            int tag_count_new = tags.Count;

            // Update listeners if we changed anything
            if (tag_count_old != tag_count_new)
            {
                Tags = TagTools.ConvertTagListToTagBundle(tags);
                return true;
            }
            return false;
        }

        private bool __RemoveTag(string dead_tag_bundle)
        {
            HashSet<string> dead_tags = TagTools.ConvertTagBundleToTags(dead_tag_bundle);

            HashSet<string> tags = TagTools.ConvertTagBundleToTags(Tags);
            int tag_count_old = tags.Count;
            foreach (string dead_tag in dead_tags)
            {
                tags.Remove(dead_tag);
            }
            int tag_count_new = tags.Count;

            if (tag_count_old != tag_count_new)
            {
                Tags = TagTools.ConvertTagListToTagBundle(tags);
                return true;
            }
            return false;
        }

        public string Tags
        {
            get
            {
                object obj = dictionary["Tags"];

                // Backwards compatibility
                {
                    List<string> tags_list = obj as List<string>;
                    if (null != tags_list)
                    {
                        FeatureTrackingManager.Instance.UseFeature(Features.Legacy_DocumentTagsList);
                        return TagTools.ConvertTagListToTagBundle(tags_list);
                    }
                }

                // Also the bundle version
                {
                    string tags_string = obj as string;
                    if (null != tags_string)
                    {
                        return tags_string;
                    }
                }

                // If we get this far, then there are no tags!  So create some blanks...
                {
                    return "";
                }
            }

            set => dictionary["Tags"] = value;
        }

        // ----------------------------------------------------------------------------------------------------

        public string DocumentBasePath => PDFDocumentFileLocations.DocumentBasePath(LibraryRef, Fingerprint);

        /// <summary>
        /// The location of the PDF on disk.
        /// </summary>
        public string DocumentPath => PDFDocumentFileLocations.DocumentPath(LibraryRef, Fingerprint, FileType);

        private bool? document_exists = null;
        public bool DocumentExists
        {
            get
            {
                if (document_exists.HasValue) return document_exists.Value;

                document_exists = File.Exists(DocumentPath);
                return document_exists.Value;
            }
        }

        private bool? document_is_corrupted = null;
        public bool IsCorruptedDocument
        {
            get
            {
                if (document_is_corrupted.HasValue) return document_is_corrupted.Value;
                return false;
            }
            set
            {
                document_is_corrupted = value;
            }
        }

        private long document_size = 0;
        public long GetDocumentSizeInBytes(long uncached_document_storage_size_override = -1)
        {
            // When the document does not exist, the size is reported as ZERO.
            // When we do not know yet whether the document exists, we'll have to go and check and find its size anyhow,
            // unless the override value is sensible, i.e. **positive**.
            //
            // Note: do NOT cache the override value!
            if (uncached_document_storage_size_override > 0)
            {
                // quick estimate override: do not check if the file exists as that will cost us dearly thanks to Disk I/O:
                if (document_size > 0) return document_size;
                return uncached_document_storage_size_override;
            }

            if (!DocumentExists) return 0;
            if (document_size > 0) return document_size;

            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            // Execute file system query and cache its result:
            document_size = File.GetSize(DocumentPath);
            return document_size;
        }

        public bool IsVanillaReference => String.Compare(FileType, Constants.VanillaReferenceFileType, StringComparison.OrdinalIgnoreCase) == 0;

        // --- Annotations / highlights / ink ----------------------------------------------------------------------

        private PDFAnnotationList annotations = null;

        public PDFAnnotationList GetAnnotations()
        {
            if (null == annotations)
            {
                WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

                annotations = new PDFAnnotationList();
                PDFAnnotationSerializer.ReadFromDisk(this);
                lock (access_lock)
                {
                    dirtyNeedsReindexing = true;
                }
            }

            return annotations;
        }

        public string GetAnnotationsAsJSON()
        {
            string json = String.Empty;

            if (null != annotations && annotations.Count > 0)
            {
                // A little hack to make sure the legacies are updated...
                foreach (PDFAnnotation annotation in annotations)
                {
                    annotation.Color = annotation.Color;
                    annotation.DateCreated = annotation.DateCreated;
                    annotation.FollowUpDate = annotation.FollowUpDate;
                }

                List<Dictionary<string, object>> attributes_list = new List<Dictionary<string, object>>();
                foreach (PDFAnnotation annotation in annotations)
                {
                    attributes_list.Add(annotation.Dictionary.Attributes);
                }
                json = JsonConvert.SerializeObject(attributes_list, Formatting.Indented);
            }
            return json;
        }

        public void AddUpdatedAnnotation(PDFAnnotation annotation)
        {
            lock (access_lock)
            {
                if (annotations.__AddUpdatedAnnotation(annotation))
            {
                    dirtyNeedsReindexing = true;
                }
            }
        }

        private PDFHightlightList highlights = null;
        public PDFHightlightList Highlights => GetHighlights(null);

        internal PDFHightlightList GetHighlights(Dictionary<string, byte[]> library_items_highlights_cache)
        {
            if (null == highlights)
            {
                WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

                lock (access_lock)
                {
                    highlights = new PDFHightlightList();
                    PDFHighlightSerializer.ReadFromStream(this, highlights, library_items_highlights_cache);
                    dirtyNeedsReindexing = true;
                }
            }

            return highlights;
        }

        public string GetHighlightsAsJSON()
        {
            string json = String.Empty;

            if (null != highlights && highlights.Count > 0)
            {
                List<PDFHighlight> highlights_list = new List<PDFHighlight>();
                foreach (PDFHighlight highlight in highlights.GetAllHighlights())
                {
                    highlights_list.Add(highlight);
                }

                json = JsonConvert.SerializeObject(highlights_list, Formatting.Indented);

                Logging.Info("Wrote {0} highlights to JSON", highlights_list.Count);
            }
            return json;
        }

        public void AddUpdatedHighlight(PDFHighlight highlight)
        {
            lock (access_lock)
            {
                if (highlights.__AddUpdatedHighlight(highlight))
            {
                    dirtyNeedsReindexing = true;
                }
            }
        }

        public void RemoveUpdatedHighlight(PDFHighlight highlight)
        {
            lock (access_lock)
            {
                highlights.__RemoveUpdatedHighlight(highlight);
                dirtyNeedsReindexing = true;
            }
        }

        private PDFInkList inks = null;
        public PDFInkList Inks => GetInks();

        internal PDFInkList GetInks()
        {
            if (null == inks)
            {
                WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

                inks = new PDFInkList();
                PDFInkSerializer.ReadFromDisk(this, inks);
                lock (access_lock)
                {
                    dirtyNeedsReindexing = true;
                }
            }

            return inks;
        }

        public byte[] GetInksAsJSON()
        {
            byte[] data = null;

            if (null != inks)
            {
                Dictionary<int, byte[]> page_ink_blobs = new Dictionary<int, byte[]>();
                foreach (var pair in inks.PageInkBlobs)
                {
                    page_ink_blobs.Add(pair.Key, pair.Value);
                }

                // We only write to disk if we have at least one page of blobbies to write...
                if (page_ink_blobs.Count > 0)
                {
                    data = SerializeFile.ProtoSaveToByteArray<Dictionary<int, byte[]>>(page_ink_blobs);
                }
            }
            return data;
        }

        public void AddPageInkBlob(int page, byte[] page_ink_blob)
        {
            if (inks.__AddPageInkBlob(page, page_ink_blob))
            {
                lock (access_lock)
                {
                    dirtyNeedsReindexing = true;
                }
            }
        }

        // -------------------------------------------------------------------------------------------------

        public void SaveToMetaData(bool force_flush_no_matter_what)
        {
            lock (access_lock)
            {
                // Save the metadata
                PDFMetadataSerializer.WriteToDisk(this, force_flush_no_matter_what);

                // Save the annotations
                PDFAnnotationSerializer.WriteToDisk(this, force_flush_no_matter_what);

                // Save the highlights
                PDFHighlightSerializer.WriteToDisk(this, force_flush_no_matter_what);

                // Save the inks
                PDFInkSerializer.WriteToDisk(this, force_flush_no_matter_what);
            }
        }

        public void CopyMetaData(PDFDocument pdf_document_template, bool copy_fingerprint = true, bool copy_filetype = true)
        {
            // prevent deadlock due to possible incorrect use of this API:
            if (pdf_document_template != this)
            {
                lock (access_lock)
                {
                    // TODO: do a proper merge, based on flags from the caller about to do and what to pass:

                    HashSet<string> keys = new HashSet<string>(dictionary.Keys);
                    foreach (var k2 in pdf_document_template.dictionary.Keys)
                    {
                        keys.Add(k2);
                    }
                    // now go through the list and see where the clashes are:
                    foreach (var k in keys)
                    {
                        if (null == dictionary[k])
                        {
                            // no collision possible: overwriting NULL or empty/non-existing slot, so we're good
                            dictionary[k] = pdf_document_template.dictionary[k];
                        }
                        else
                        {
                            object o1 = dictionary[k];
                            object o2 = pdf_document_template.dictionary[k];
                            string s1 = o1?.ToString();
                            string s2 = o2?.ToString();
                            string t1 = o1?.GetType().ToString();
                            string t2 = o2?.GetType().ToString();
                            if (s1 == s2 && t1 == t2)
                            {
                                // values match, so no change. We're golden.
                            }
                            else
                            {
                                Logging.Warn("Copying/Moving metadata into {0}: collision on key {1}: old value = ({4})'{2}', new value = ({5})'{3}'", this.Fingerprint, k, s1, s2, t1, t2);

                                // TODO: when this is used for merging metadata anyway...
                                switch (k)
                                {
                                    case "DateAddedToDatabase":
                                        // take oldest date:
                                        break;

                                    case "DateLastModified":
                                        // take latest, unless the last mod dates match the DateAddedToDatabase records: in that case, use the picked DateAddedToDatabase
                                        break;

                                    case "FileType":
                                        // do not copy old value into current record?
                                        if (copy_filetype)
                                        {
                                            dictionary[k] = pdf_document_template.dictionary[k];
                                        }
                                        break;

                                    case "Fingerprint":
                                        // do not copy old value into current record?
                                        if (copy_fingerprint)
                                        {
                                            dictionary[k] = pdf_document_template.dictionary[k];
                                        }
                                        break;
                                }
                            }
                        }
                    }

                    dictionary["ColorWrapper"] = pdf_document_template.dictionary["ColorWrapper"];
                    dictionary["DateAddedToDatabase"] = pdf_document_template.dictionary["DateAddedToDatabase"];
                    dictionary["DateLastCited"] = pdf_document_template.dictionary["DateLastCited"];
                    dictionary["DateLastModified"] = pdf_document_template.dictionary["DateLastModified"];
                    dictionary["DateLastRead"] = pdf_document_template.dictionary["DateLastRead"];
                    dictionary["AbstractOverride"] = pdf_document_template.dictionary["AbstractOverride"];
                    dictionary["Authors"] = pdf_document_template.dictionary["Authors"];
                    dictionary["AuthorsSuggested"] = pdf_document_template.dictionary["AuthorsSuggested"];
                    dictionary["AutoSuggested_BibTeXSearch"] = pdf_document_template.dictionary["AutoSuggested_BibTeXSearch"];
                    dictionary["AutoSuggested_OCRFrontPage"] = pdf_document_template.dictionary["AutoSuggested_OCRFrontPage"];
                    dictionary["AutoSuggested_PDFMetadata"] = pdf_document_template.dictionary["AutoSuggested_PDFMetadata"];
                    dictionary["BibTex"] = pdf_document_template.dictionary["BibTex"];
                    dictionary["Bookmarks"] = pdf_document_template.dictionary["Bookmarks"];
                    dictionary["Comments"] = pdf_document_template.dictionary["Comments"];
                    dictionary["Deleted"] = pdf_document_template.dictionary["Deleted"];
                    dictionary["DownloadLocation"] = pdf_document_template.dictionary["DownloadLocation"];
                    if (copy_filetype)
                    {
                        dictionary["FileType"] = pdf_document_template.dictionary["FileType"];
                    }
                    if (copy_fingerprint)
                    {
                        dictionary["Fingerprint"] = pdf_document_template.dictionary["Fingerprint"];
                    }
                    dictionary["HaveHardcopy"] = pdf_document_template.dictionary["HaveHardcopy"];
                    dictionary["IsFavourite"] = pdf_document_template.dictionary["IsFavourite"];
                    dictionary["PageLastRead"] = pdf_document_template.dictionary["PageLastRead"];
                    dictionary["Rating"] = pdf_document_template.dictionary["Rating"];
                    dictionary["ReadingStage"] = pdf_document_template.dictionary["ReadingStage"];
                    dictionary["Tags"] = pdf_document_template.dictionary["Tags"];
                    dictionary["Title"] = pdf_document_template.dictionary["Title"];
                    dictionary["TitleSuggested"] = pdf_document_template.dictionary["TitleSuggested"];
                    dictionary["Year"] = pdf_document_template.dictionary["Year"];
                    dictionary["YearSuggested"] = pdf_document_template.dictionary["YearSuggested"];

                    annotations = (PDFAnnotationList)pdf_document_template.GetAnnotations().Clone();
                    highlights = (PDFHightlightList)pdf_document_template.Highlights.Clone();
                    inks = (PDFInkList)pdf_document_template.Inks.Clone();
                }
            }
        }

        /// <summary>
        /// NB: only call this as part of document creation.
        /// </summary>
        public void CloneMetaData(PDFDocument existing_pdf_document)
        {
            // prevent deadlock due to possible incorrect use of this API:
            if (existing_pdf_document != this)
            {
                Logging.Warn("TODO: CloneMetaData: MERGE metadata for existing document and document which was copied/moved into this library. Target Document: {0}, Source Document: {1}", this.Fingerprint, existing_pdf_document.LibraryRef);

                lock (existing_pdf_document.access_lock)
                {
                    lock (access_lock)
                    {
                        bindable = null;

                        Logging.Info("Cloning metadata from {0}: {1}", existing_pdf_document.Fingerprint, existing_pdf_document.TitleCombined);

                        //dictionary = (DictionaryBasedObject)existing_pdf_document.dictionary.Clone();
                        CopyMetaData(existing_pdf_document);

                        // Copy the citations
                        PDFDocumentCitationManager.CloneFrom(existing_pdf_document.PDFDocumentCitationManager);

                        QueueToStorage();

#if false
                        SaveToMetaData();

                        //  Now clear out the references for the annotations and highlights, so that when they are reloaded the events are resubscribed
                        annotations = null;
                        highlights = null;
                        inks = null;
#endif
                    }
                }
            }
        }

        public void StoreAssociatedPDFInRepository(string filename)
        {
            if (File.Exists(filename) && !File.Exists(DocumentPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DocumentPath));
                Alphaleonis.Win32.Filesystem.File.Copy(filename, DocumentPath);
            }
        }

        public override string ToString()
        {
            return Fingerprint;
        }

        internal PDFAnnotation GetAnnotationByGuid(Guid guid)
        {
            foreach (PDFAnnotation pdf_annotation in GetAnnotations())
            {
                if (pdf_annotation.Guid == guid)
                {
                    return pdf_annotation;
                }
            }

            return null;
        }

        internal PDFDocument AssociatePDFWithVanillaReference_Part1(string pdf_filename, WebLibraryDetail web_library_detail)
        {
            // Only works with vanilla references
            if (!IsVanillaReference)
            {
                throw new Exception("You can only associate a PDF with a vanilla reference.");
            }

            // Create the new PDF document
            PDFDocument new_pdf_document = LibraryRef.Xlibrary.AddNewDocumentToLibrary_SYNCHRONOUS(pdf_filename, web_library_detail, pdf_filename, pdf_filename, null, null, null, false);

            return new_pdf_document;
        }
    }
}
