using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using Tailgrab.Clients.Prismic;
using Tailgrab.PlayerManagement;

namespace Tailgrab.Clients.Office
{
    public class OfficeClient
    {
        private readonly ServiceRegistry _serviceRegistry;

        public OfficeClient(ServiceRegistry serviceRegistry) 
        { 
            _serviceRegistry = serviceRegistry;
            ExcelPackage.License.SetNonCommercialOrganization("Tailgrab");
        }

        public byte[] ExportAvatarsToExcel(List<UserAvatarViewModel> avatars, string sheetName)
        {
            // Ensure we reference the EPPlus types from the global namespace to avoid collision with this namespace
            using (ExcelPackage package = new ExcelPackage())
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(sheetName);
                // ── Header row ──
                string[] headers = { "Name", "Author", "AvatarId", "Description", "Platforms", "Imposter", "PC Rating", "Quest Rating", "IOS Rating", "Content Warnings", "Style Filter", "Marketplace" };
                for (int c = 0; c < headers.Length; c++)
                    worksheet.Cells[1, c + 1].Value = headers[c];

                // Bold the header
                worksheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;

                worksheet.Column(1).Width = 200;

                // ── Data rows ──
                int row = 2;
                foreach (UserAvatarViewModel vm in avatars)
                {
                    AvatarEntry r = vm.AvatarEntry;
                    worksheet.Row(row).Height = 100;
                    worksheet.Cells[row, 1].Formula = "IMAGE(\"" + vm.ThumbnailUrl + "\", 1)";
                    worksheet.Cells[row, 2].Value = r?.Name;
                    worksheet.Cells[row, 3].Value = r?.Author;
                    worksheet.Cells[row, 4].Value = r?.AvatarId;
                    worksheet.Cells[row, 5].Value = r?.Description;
                    worksheet.Cells[row, 6].Value = r?.Platform;
                    worksheet.Cells[row, 7].Value = r?.Impostor;
                    worksheet.Cells[row, 8].Value = r?.PCRating;
                    worksheet.Cells[row, 9].Value = r?.QuestRating;
                    worksheet.Cells[row, 10].Value = r?.IOSRating;
                    worksheet.Cells[row, 11].Value = r?.ContentWarnings;
                    worksheet.Cells[row, 12].Value = r?.StyleFilter;
                    worksheet.Cells[row, 13].Value = r?.Marketplace;
                    worksheet.Cells[row, 14].Value = vm.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[row, 15].Value = vm.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[row, 16].Value = vm.ThumbnailUrl;
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
