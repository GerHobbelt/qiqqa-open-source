# Document metadata - flattened table or what?

Current Qiqqa (v83 and older) dump the Qiqqa metadata as a single BibTeX wrapped in JSON *record* in the one-for-all table that's the current database layout.

*We* intend to have *versioning* added, so we can slowly improve our metadata, either as a whole or in part, e.g. by correcting typos in the document title and then *updating* our metadata database: later on we SHOULD be able to observe that attribute update as part of a revision list - a bit a la `git log`. You get the idea.

Part of that *gradual improvement process* is tagging the updates with a reliability / believability / **feducy** rating: auto-guesstimated metadata should be ranked rather low, until a human has inspected and *vetted* the data, in which case it will see a significant jump in *feducy ranking*. Specialized (semi-)automated metadata improvement processes MAY bump up the rank a bit here and there, if we find that idea actually useful in practice, e.g. e.g. bumping up the ranking when we've discovered the metadata element matches for several (deemed *independent*) sources? Or when we have spell-checked the Abstract or blurbs in a fully automated pre-process? (So we wouldn't have to do so much vetting/correcting by hand any more, we hope.) Or specialized user-controlled scripts have gone over the available metadata and submitted their own (derived) set as the conclusion to their activity -- assuming they *add* to the overall metadata quality and hence deserve a slight bump in their metadata records' ranking too!
There's also our *multiple sources for (nearly) the same stuff* conundrum to consider here: we've observed there's multiple BibTeX records available out there (Google Scholar being only one of them), with various and *varying* degree of completeness and other marks of quality. For a single document. So we wish to import some or all of those "*variants*" / *alternatives* and mix & mash them as we, the *user*, may see fit. Consequently, we need to record the source / origin for each *element* of our metadata and at the same (low) ranking we MAY expect multiple entries (records?) from different origins. Our final metadata produce COULD be a mix/mash of these multiple sources.
Meanwhile I want to be able to offer users the ability to query the database directly (when they're up for it, technically) and then query for a title, author or other metadata *element* match. This implies we're to move from a wrapper-in-a-wrapper whole-record-dump to a per-element database table layout. This speaks strongly for a *normalized* database table layout, e.g. key:(document ID, metadata item name/ID, source ID, ranking) + data:(ranking?, version?,  metadata value). Thus we would expect to have several tens of records per document. Which implies we have to reckon with a *large* metadata table with a non-unique index and all the performance worries such a beast entails...

One of the ideas there is to have such a table as a "historical record/log" and acceptably slow, while we also would sport a flattened, high-performance, table which only ever stores the latest & greatest of the collected metadata wisdom. The latter table is then to be used for all the regular Qiqqa activities, while the revision history table serves as a undo/step-back and metadata *developmental analysis* support tool, perhaps?

But what abut the plethora of different metadata elements we're anticipating? And not just the usual suspects when it comes to BibTeX records and *citations*, but all that goody goodness that can be used to perform meta-analyses on your collection: *abstract* and possibly also *document text content* itself MAY give *acte de presance* here! And these both merit rank/revision tracking for sure as we've seen plenty of mistakes in that data blurb that deserves correction, either automatically or manually: even picking the correct chunk of content for the *abstract* has turned out to be a real challenge for the automatons, so we can expect updates/corrections for these. Hence: *revisions*. With probably improved *ranking*!

Meanwhile, we also have multiple documents, from different origins, which essentially carry the same content, e.g. ArXiv document versions or preprints vs. published versions. We have documents which have been published in multiple venues and are all ever so slightly different. Meanwhile, we COULD use *parts* of their brethren's metadata elements to starkly improve each' metadata set: I have also observed that the latest (best?) document version DID NOT come with the best bibTeX metadata record. *Au contraire, mon ami.* Google Scholar often is *not* the deliverer of best quality BibTeX/metadata, I find. So it can happen that the most complete BibTeX comes linked to a preprint edition, while some of the journal-published versions are hard pressed to produce/match a mediocre-or-worse metadata record. Hence we must reckon with the ranking not always increasing while we import new sources and consequently reprocess the metadata set. Or so the thinking goes...

The bit that hasn't been addressed in detail yet is the question: great ideas all, sure, but what's the precise, *performant!*, query which can deliver the top-ranking metadata collective for a document?... because some metadata elements may have *quite different* developmental histories and rankings than others. I wouldn't be surprised to see a high-quality title+author in there, thanks to a high-ranking source and/or some user vetting, while, say, the abstract and content attributes have rather low rankings: auto-extracted by way of OCR and not a lot of **feducy** there, as no human has spent the time and effort yet to vet & edit/correct those buggers. Which we want mirrored in that query we asked for: all his goodness is only great to dig through by way of user-written scripts, etc., the way you see meta-research executed, when such queries are actually doable and *reasonably fast* for these rather large database tables. The alternative would be flattened tables and/or custom UDFs (User Defined functions) to match against specific elements and/or pick the top ranking revisions.

Incidentally, flattening the table, while certainly one way to *potentially successfully* approach the performance question, is perceived as a rather low-quality answer as we SHOULD expect metadata attributes to update/improve at arbitrary moments and not *all together now* but rather more haphazardly as the user tends to focus their update/change/vet process on particular subsets, I expect. Let alone the effects of intermediate additional imports when the user finds another *second source* for the document: one more URL metadata entry and possibly some other metadata fields' base revisions as we add he new incoming metadata o the database: should we welcome every little update/change with the submit of a fresh *complete* (flattened) record? Where we store a flattened set of elements,. their rankings, sources and values. And where, thanks to the flattening, we *implicitly* assume there's always, at all times, only a single best answer? 
Or should we forego *flattening* like that and have the "*pick the top ranking element*" action for each element one in our own software, perhaps? A solution which would make it possibly harder for other folks who wish to talk to the database directly to talk and get these kind of desirable "best of" answers, unless they travel through our custom process. Not a very nice way either, so we'll look for a *cool*, *hot* SQL query + indexing scheme first. 😉😅
