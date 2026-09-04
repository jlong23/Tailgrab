using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using Tailgrab.Clients.Prismic;

namespace Tailgrab.Clients.Office
{
    public class OfficeClient
    {
        public static byte[] CreateExcelFile(string sheetName, AvatarsLookupResponse response)
        {

            // Ensure we reference the EPPlus types from the global namespace to avoid collision with this namespace
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using (ExcelPackage package = new ExcelPackage())
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(sheetName);
                // ── Header row ──
                string[] headers = { "Name", "Author", "AvatarId", "Description", "Platforms", "Imposter", "PC Rating", "Quest Rating", "IOS Rating", "Content Warnings", "Style Filter", "Marketplace" };
                for (int c = 0; c < headers.Length; c++)
                    worksheet.Cells[1, c + 1].Value = headers[c];

                // Bold the header
                worksheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;

                // ── Data rows ──
                int row = 2;
                foreach (AvatarsLookupListResponse r in response.Results)
                {
                    worksheet.Cells[row, 1].Value = r.Data.Name;
                    worksheet.Cells[row, 2].Value = r.Data.Author;
                    worksheet.Cells[row, 3].Value = r.Data.AvatarId;
                    worksheet.Cells[row, 4].Value = r.Data.Description;
                    worksheet.Cells[row, 5].Value = r.Data.Platform;
                    worksheet.Cells[row, 6].Value = r.Data.Impostor;
                    worksheet.Cells[row, 7].Value = r.Data.PCRating;
                    worksheet.Cells[row, 8].Value = r.Data.QuestRating;
                    worksheet.Cells[row, 9].Value = r.Data.IOSRating;
                    worksheet.Cells[row, 10].Value = r.Data.ContentWarnings;
                    worksheet.Cells[row, 11].Value = r.Data.StyleFilter;
                    worksheet.Cells[row, 12].Value = r.Data.Marketplace;
                    row++;
                }

                // Auto-fit column widths
                worksheet.Cells.AutoFitColumns();

                Byte[] spreadSheet = package.GetAsByteArray();
                return spreadSheet;
            }
        }
    }
}
