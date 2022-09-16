﻿namespace Utilities.PDF.Sorax
{
    public class SoraxPDFRenderer
    {
        private SoraxPDFRendererCache cache = new SoraxPDFRendererCache();
        private string pdf_filename;
        private string pdf_user_password;
        private string pdf_owner_password;

        public SoraxPDFRenderer(string pdf_filename, string pdf_user_password, string pdf_owner_password)
        {
            this.pdf_filename = pdf_filename;
            this.pdf_user_password = pdf_user_password;
            this.pdf_owner_password = pdf_owner_password;
        }

        // ------------------------------------------------------------------------------------------------------------------------

        public byte[] GetPageByHeightAsImage(int page, double height)
        {
            // Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (cache)
            {
                // l1_clk.LockPerfTimerStop();
                byte[] bitmap = cache.Get(page, height);
                if (null == bitmap)
                {
                    bitmap = SoraxPDFRendererDLLWrapper.GetPageByHeightAsImage(pdf_filename, pdf_owner_password, pdf_user_password, page, height);
                    cache.Put(page, height, bitmap);
                }
                return bitmap;
            }

        }

        public byte[] GetPageByDPIAsImage(int page, float dpi)
        {
            return SoraxPDFRendererDLLWrapper.GetPageByDPIAsImage(pdf_filename, pdf_owner_password, pdf_user_password, page, dpi);
        }

        public void Flush()
        {
            // Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (cache)
            {
                // l1_clk.LockPerfTimerStop();
                cache.Flush();
            }
        }
    }
}
