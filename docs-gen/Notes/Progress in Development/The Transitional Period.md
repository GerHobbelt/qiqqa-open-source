# The Transitional Period

This is about getting from current Qiqqa to future Qiqqa.

"*Old Qiqqa*" is: 

* WPF for the GUI
  * using commercial library (Infragistics) for some parts of GUI
* Windows-only application
* old Lucene.NET v2.x
* obsolete XULrunner (~ Firefox version 36)
* old `mudraw` (v1.11 or there-abouts, *patched*)
* commercial PDF render lib for viewing/reading (SORAX company is the **defunct manufacturer**)
* flaky text extraction by way of QiqqaOCR
* *.NET specific serialization* of important structures (configuration, PDF annotations) to disk & database.
* SQLite metadata database via SQLite.NET; has sync to cloud storage/NAS/network fatal issues.
* 32-bit application only (restricted to 32bit by the libraries used)
* Trouble with libraries in the 10K's

"*Future Qiqqa*" is: 

* Cross-platform GUI: web-based technology (HTML/CSS/JS) + wxWidgets for the simpler / *fast* UI bits
* Windows + Linux natively supported. Apple/Mac must be very doable as a "opinionated Linux version"
* SOLR for searching (also open to direct *user access*, enabling everyone to do advanced stuff with their data)
* CEF (tracking Chromium releases as closely as possible for latest browser developments).
  * wxWebView (EDGE/...) as an alternative/intermediate step as wxChromium isn't well supported...
* bleeding edge MuPDF + *patches* + Tesseract for all PDF work, including reading/viewing.
* SQLite metadata database (opened up to enable user-written scripts to work the data for *advanced usage*)
* Revamped NAS/network/cloud Sync for cooperative & backup work on a single library.
* *64bit first* (maybe a 'older boxes' 32bit build alongside?)
* Copes well with 100K+ libraries on medium hardware.

## Tackling the transition from 32bit to 64bit

Experiments have shown that I have no stable way on Windows to start 64bit executables from a 32bit binary. This restricts all backend changes (including QiqqaOCR *full* or *partial* replacements) to having to be 32bit builds.

Further tests have shown repeatedly (and very recently *again*) that I have *unsolved problems* invoking external applications from the .NET application, where I need very tight control over those external application's stdin+stdout+stderr streams, including *binary data* transmissions.

This has led to a few conclusions and *partial solutions* that will be developed further:

* Anything that's currently the equivalent of an `execv` (i.e. `.exe` program invoke + stdout&stderr *redirect*) must be converted to a socket connection, using *any* protocol I like for transferring data. 
  
  * This includes replacing the SORAX linked-in *library* with a *latency-risk-introducing client-server round-trip* to a MuPDF-based PDF render server which will spit out huge amounts of PDF page *images* when a user is browsing / reading their PDF documents.
  
  * As I've made the *design choice* to use *multiple programming languages*, using the major packages for each technology, there's the added problem of interfacing those. 
    
     > 
     > * Search: SOLR = Java, 
     > * Render: MuPDF = C
     > * OCR = Tesseract = C++
     > * Text Extract = MuPDF = C
     > * DB = SQLite = C
     > * GUI = Chrome/Chromium = HTML/CSS/JS
     > * ... relegating C# / .NET to a *business logic / glue* role, which is *sustainable* cross-platform as this means C#/.NET is no longer *directly involved in the GUI*. (Avalonia would have been an *GUI option* if I personally would *like* using WPF, which I *certainly do not*, alas.)
     > Several technologies exist to integrate most in a single application (by way of a collection of DLLs) but providing that cross-platform in a *stable* manner is *hard* and *fickle*. 
     > 
     > The *consequential choice* thus is to *not integrate* but use (*local*) client-server socket-based loose interfaces at programming language boundaries instead.
     > x
     > This introduces a new problem: *increased latency risk* i.e. *reduced responsiveness of the user-facing application*, resulting in a worsened UX.
     > 
     >  > 
     >  > The additional risk thus introduced is *inadvertent access to the server components from outside the machine* is to be solved by ensuring these server components *only bind to localhost 127.0.0.1 and never to 0.0.0.0*.

* In order to fix the current 32bit *lock-in*, we need to provide a 64bit *launcher*, which will be able to both:
  
  * start (and *monitor*!) the upcoming 64bit server components, and
  * start the current 32bit Qiqqa application, as is it is kept alive and *migrated* by shedding the above components one-by-one.
  I expect we'll need to *monitor* the Qiqqa/.NET app as well to make sure our launcher has a lifetime controlled by the Qiqqa GUI = *user facing* UI application. Ditto for the backend server components, unless I find any or all of those *stable enough for long running*.

## See Also

* [Qiqqa and inter-process communications (IPC) - old and new](Considering%20the%20Way%20Forward/IPC/Qiqqa%20and%20inter-process%20communications%20%28IPC%29%20-%20old%20and%20new.md)
  * [Who should be able to access what directly](Considering%20the%20Way%20Forward/IPC/Who%20should%20be%20able%20to%20access%20what%20directly.md)
  * [Considering IPC methods - HTTP vs WebSocket, Pipe, etc](Considering%20the%20Way%20Forward/IPC/Considering%20IPC%20methods%20-%20HTTP%20vs%20WebSocket,%20Pipe,%20etc.md)
* [The Transitional Period - Extra Notes](The%20Transitional%20Period%20-%20Extra%20Notes.md)