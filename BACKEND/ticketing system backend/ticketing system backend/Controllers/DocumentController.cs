using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ticketing_system_backend.Models;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using BarcodeWriter = ZXing.BarcodeWriter<System.Drawing.Bitmap>;
// Define type aliases to resolve namespace conflicts
using SyncfusionPdfDocument = Syncfusion.Pdf.PdfDocument;
using SyncfusionSizeF = Syncfusion.Drawing.SizeF;
using SystemBitmap = System.Drawing.Bitmap;
using SystemImageFormat = System.Drawing.Imaging.ImageFormat;

namespace ticketing_system_backend.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class DocumentController : ControllerBase
  {
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly ILogger<DocumentController> _logger;
    private readonly string _dynamicImagesPath;


    public DocumentController(IWebHostEnvironment hostingEnvironment, ILogger<DocumentController> logger)
    {
      _hostingEnvironment = hostingEnvironment;
      _logger = logger;
      _dynamicImagesPath = Path.Combine(_hostingEnvironment.ContentRootPath, "DynamicDescriptionImages");
      if (!Directory.Exists(_dynamicImagesPath))
      {
        Directory.CreateDirectory(_dynamicImagesPath);
      }
    }

    [HttpGet("generate-label-compact")]
    public IActionResult GenerateLabelCompact(
    string poNumber = "12390/FM250701",
    string barcodeValue = "0272072500001",
    string date = "07/25",
    string code = "FGS-019")
    {
      using MemoryStream ms = new MemoryStream();

      Rectangle labelSize = new Rectangle(127.56f, 70.86f); // 45x25mm
      Document doc = new Document(labelSize, 0, 0, 0, 0);
      PdfWriter writer = PdfWriter.GetInstance(doc, ms);
      doc.Open();
      PdfContentByte cb = writer.DirectContent;

      var poFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 6);
      var tinyFont = FontFactory.GetFont(FontFactory.HELVETICA, 4);

      // PO Number Top Center
      ColumnText.ShowTextAligned(cb,
          Element.ALIGN_CENTER,
          new Phrase($"PO# {poNumber}", poFont),
          65,58, 0);

      // Barcode setup (thicker + taller bars)
      Barcode128 barcode = new Barcode128
      {
        Code = barcodeValue,
        Font = null,
        BarHeight = 100f, // increase bar height
        X = 2f         // increase bar thickness (default ~1.0)
      };

      Image bcImage = barcode.CreateImageWithBarcode(cb, null, null);

      // Make barcode BIGGER
      bcImage.ScaleToFit(100f, 40f);
      bcImage.SetAbsolutePosition(15, 17);

      doc.Add(bcImage);

      // Barcode text right below barcode
      ColumnText.ShowTextAligned(cb,
          Element.ALIGN_CENTER,
          new Phrase(barcodeValue, tinyFont),
          labelSize.Width / 2, 12, 0);

      // Date bottom-left
      ColumnText.ShowTextAligned(cb,
          Element.ALIGN_LEFT,
          new Phrase(date, tinyFont),
          3, 3, 0);

      // Code bottom-right
      ColumnText.ShowTextAligned(cb,
          Element.ALIGN_RIGHT,
          new Phrase(code, tinyFont),
          labelSize.Width - 3, 3, 0);

      doc.Close();

      return File(ms.ToArray(), "application/pdf", $"Label-{barcodeValue}.pdf");
    }

    [HttpGet("generate-label-compactS")]
    public IActionResult GenerateLabelCompactS(
    [FromQuery] string poNumber = "12390/FM250701",
    [FromQuery] string eanCode = "02720725",
    [FromQuery] string date = "07/25",
    [FromQuery] string? code ="FGS-019",
    [FromQuery] int startSerial = 1,
    [FromQuery] int endSerial = 10)
    {
      using MemoryStream ms = new MemoryStream();

      Rectangle labelSize = new Rectangle(127.56f, 70.86f); // 45x25mm
      Document doc = new Document(labelSize, 0, 0, 0, 0);
      PdfWriter writer = PdfWriter.GetInstance(doc, ms);
      doc.Open();

      var poFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8);
      var tinyFont = FontFactory.GetFont(FontFactory.HELVETICA, 6);

      for (int serial = startSerial; serial <= endSerial; serial++)
      {
        string serialStr = serial.ToString("D4"); // 0001, 0002...

        // Build barcode: eanCode + SerialNo
        // Example: 02720725 + 0001 = 027207250001
        string fullBarcodeValue = eanCode + serialStr;

        if (serial > startSerial)
          doc.NewPage();

        PdfContentByte cb = writer.DirectContent;

        // PO Number Top Center
        ColumnText.ShowTextAligned(cb,
            Element.ALIGN_CENTER,
            new Phrase($"PO# {poNumber}", poFont),
            labelSize.Width / 2, 58, 0);

        // Barcode setup
        Barcode128 barcode = new Barcode128
        {
          Code = fullBarcodeValue,
          Font = null,
          BarHeight = 100f,
          X = 2f
        };

        // Create Barcode image
        Image bcImage = barcode.CreateImageWithBarcode(cb, null, null);
        bcImage.ScaleToFit(100f, 40f);
        bcImage.SetAbsolutePosition(15, 17);
        doc.Add(bcImage);

        // Barcode text
        ColumnText.ShowTextAligned(cb,
            Element.ALIGN_CENTER,
            new Phrase(fullBarcodeValue, tinyFont),
            labelSize.Width / 2 - 5, 12, 0);

        // Date bottom-left
        ColumnText.ShowTextAligned(cb,
            Element.ALIGN_LEFT,
            new Phrase(date, tinyFont),
            3, 3, 0);

        // Code bottom-right
        ColumnText.ShowTextAligned(cb,
            Element.ALIGN_RIGHT,
            new Phrase(code, tinyFont),
            labelSize.Width - 3, 3, 0);
      }

      doc.Close();
      return File(ms.ToArray(), "application/pdf", $"Labels-{startSerial}-{endSerial}.pdf");
    }

    [HttpGet("generate-medical-label")]
    public IActionResult GenerateMedicalLabel(
    [FromQuery] string refNo = "REF12345",
    [FromQuery] string serialNo = "SN67890",
    [FromQuery] string manufactureDate = "11/25",
    [FromQuery] string typeNo = "T001",
    [FromQuery] string barcodeNumber = "1234567890123")
    {
      try
      {
        byte[] pdfBytes = CreateMedicalLabelPdf(refNo, serialNo, manufactureDate, typeNo, barcodeNumber);
        return File(pdfBytes, "application/pdf", $"MedicalLabel_{serialNo}.pdf");
      }
      catch (Exception ex)
      {
        return StatusCode(500, $"Error generating PDF: {ex.Message}");
      }
    }

    private byte[] CreateMedicalLabelPdf(string refNo, string serialNo, string manufactureDate, string typeNo, string barcodeNumber)
    {
      float widthInPoints = 67.25f * 2.83465f;  // 67.25mm to points
      float heightInPoints = 32f * 2.83465f;    // 32mm to points

      using (MemoryStream ms = new MemoryStream())
      {
        Document document = new Document(new Rectangle(widthInPoints, heightInPoints), 3, 3, 3, 3);
        PdfWriter writer = PdfWriter.GetInstance(document, ms);
        document.Open();

        PdfPTable mainTable = new PdfPTable(4);
        mainTable.WidthPercentage = 100;
        mainTable.SetWidths(new float[] { 15f, 25f, 30f, 30f });

        // Row 1: Symbols
        mainTable.AddCell(CreateSymbolCell());
        mainTable.AddCell(CreateEmptyCell());
        mainTable.AddCell(CreateSymbolCell2());
        mainTable.AddCell(CreateSymbolCell3());

        // Row 2: REF Number
        PdfPTable refTable = new PdfPTable(1);
        refTable.AddCell(CreateLabelCell("REF", 8));
        refTable.AddCell(CreateValueCell(refNo, 7));
        mainTable.AddCell(CreateNestedTableCell(refTable, 2));
        mainTable.AddCell(CreateEmptyCell());
        mainTable.AddCell(CreateEmptyCell());

        // Row 3: Serial Number
        PdfPTable snTable = new PdfPTable(1);
        snTable.AddCell(CreateLabelCell("SN", 8));
        snTable.AddCell(CreateValueCell(serialNo, 7));
        mainTable.AddCell(CreateNestedTableCell(snTable, 2));
        mainTable.AddCell(CreateEmptyCell());
        mainTable.AddCell(CreateEmptyCell());

        // Row 4: Manufacturing Date
        PdfPTable dateTable = new PdfPTable(1);
        dateTable.AddCell(CreateLabelCell("DATE", 8));
        dateTable.AddCell(CreateValueCell(manufactureDate, 7));
        mainTable.AddCell(CreateNestedTableCell(dateTable, 2));
        mainTable.AddCell(CreateLotCell("LOT", 8));
        mainTable.AddCell(CreateEmptyCell());

        // Row 5: Type
        mainTable.AddCell(CreateValueCell($"Type {typeNo}", 7));
        mainTable.AddCell(CreateEmptyCell());
        mainTable.AddCell(CreateEmptyCell());
        mainTable.AddCell(CreateEmptyCell());

        // Row 6: Barcode (larger space)
        PdfPCell barcodeCell = CreateBarcodeCell(barcodeNumber, writer);
        barcodeCell.Colspan = 2;
        barcodeCell.MinimumHeight = 25f;
        mainTable.AddCell(barcodeCell);
        mainTable.AddCell(CreateEmptyCell());
        mainTable.AddCell(CreateEmptyCell());

        document.Add(mainTable);
        document.Close();
        writer.Close();

        return ms.ToArray();
      }
    }

    private PdfPCell CreateSymbolCell()
    {
      PdfPCell cell = new PdfPCell(new Phrase("CE", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8)));
      cell.HorizontalAlignment = Element.ALIGN_CENTER;
      cell.VerticalAlignment = Element.ALIGN_MIDDLE;
      cell.Border = Rectangle.NO_BORDER;
      cell.MinimumHeight = 12f;
      return cell;
    }

    private PdfPCell CreateSymbolCell2()
    {
      PdfPCell cell = new PdfPCell(new Phrase("℞", FontFactory.GetFont(FontFactory.HELVETICA, 10)));
      cell.HorizontalAlignment = Element.ALIGN_CENTER;
      cell.VerticalAlignment = Element.ALIGN_MIDDLE;
      cell.Border = Rectangle.NO_BORDER;
      return cell;
    }

    private PdfPCell CreateSymbolCell3()
    {
      Paragraph p = new Paragraph();
      p.Add(new Chunk("⚠", FontFactory.GetFont(FontFactory.HELVETICA, 8)));
      p.Add(new Chunk("\n!", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 6)));

      PdfPCell cell = new PdfPCell(p);
      cell.HorizontalAlignment = Element.ALIGN_CENTER;
      cell.VerticalAlignment = Element.ALIGN_MIDDLE;
      cell.Border = Rectangle.NO_BORDER;
      return cell;
    }

    private PdfPCell CreateLabelCell(string text, float fontSize)
    {
      PdfPCell cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, fontSize)));
      cell.HorizontalAlignment = Element.ALIGN_LEFT;
      cell.VerticalAlignment = Element.ALIGN_MIDDLE;
      cell.Border = Rectangle.NO_BORDER;
      cell.PaddingLeft = 2f;
      cell.MinimumHeight = 10f;
      return cell;
    }

    private PdfPCell CreateLotCell(string text, float fontSize)
    {
      PdfPCell cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, fontSize)));
      cell.HorizontalAlignment = Element.ALIGN_CENTER;
      cell.VerticalAlignment = Element.ALIGN_MIDDLE;
      cell.Border = Rectangle.NO_BORDER;
      return cell;
    }

    private PdfPCell CreateValueCell(string text, float fontSize)
    {
      PdfPCell cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA, fontSize)));
      cell.HorizontalAlignment = Element.ALIGN_LEFT;
      cell.VerticalAlignment = Element.ALIGN_MIDDLE;
      cell.Border = Rectangle.NO_BORDER;
      cell.PaddingLeft = 2f;
      cell.MinimumHeight = 10f;
      return cell;
    }

    private PdfPCell CreateBarcodeCell(string barcodeText, PdfWriter writer)
    {
      Barcode128 barcode = new Barcode128
      {
        Code = barcodeText,
        Font = null,
        BarHeight = 20f,
        X = 1.5f
      };

      Image barcodeImage = barcode.CreateImageWithBarcode(writer.DirectContent, null, null);
      barcodeImage.ScaleToFit(90f, 22f);

      PdfPCell cell = new PdfPCell(barcodeImage, true);
      cell.HorizontalAlignment = Element.ALIGN_LEFT;
      cell.VerticalAlignment = Element.ALIGN_MIDDLE;
      cell.Border = Rectangle.NO_BORDER;
      cell.PaddingLeft = 2f;
      cell.PaddingTop = 2f;
      return cell;
    }

    private PdfPCell CreateEmptyCell()
    {
      PdfPCell cell = new PdfPCell(new Phrase(""));
      cell.Border = Rectangle.NO_BORDER;
      return cell;
    }

    private PdfPCell CreateNestedTableCell(PdfPTable table, int colspan = 1)
    {
      PdfPCell cell = new PdfPCell(table);
      cell.Border = Rectangle.NO_BORDER;
      cell.Colspan = colspan;
      cell.Padding = 0;
      return cell;
    }
    //[HttpGet("generate-labels")]
    //public IActionResult GenerateLabels(
    //[FromQuery] string poNumber = "13100/FM251204",
    //[FromQuery] string eanCode = "027212250",
    //[FromQuery] string date = "12/25",
    //[FromQuery] string code = "FGS-019",
    //[FromQuery] int startSerial = 601,
    //[FromQuery] int endSerial = 1000)
    //{
    //  // --- Setup memory and document ---
    //  using MemoryStream ms = new MemoryStream();

    //  // Label size: 70 mm × 25 mm
    //  float MmToPt(float mm) => mm * 72f / 25.4f;
    //  Rectangle labelSize = new Rectangle(MmToPt(70f), MmToPt(25f));
    //  Document doc = new Document(labelSize, 0, 0, 0, 0);
    //  PdfWriter writer = PdfWriter.GetInstance(doc, ms);
    //  doc.Open();

    //  BaseFont bf = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, false);
    //  PdfContentByte cb = writer.DirectContent;

    //  // --- Loop through serial numbers ---
    //  for (int serial = startSerial; serial <= endSerial; serial++)
    //  {
    //    // Build EAN = ITEMNO + MM + YY + SERIALNO
    //    string[] dateParts = date.Split('/');
    //    string mm = dateParts.Length > 0 ? dateParts[0] : "00";
    //    string yy = dateParts.Length > 1 ? dateParts[1] : "00";
    //    string serialStr = serial.ToString("D5");
    //    string fullEan = $"{eanCode}{mm}{yy}{serialStr}";

    //    if (serial > startSerial)
    //      doc.NewPage();

    //    float pageW = labelSize.Width;
    //    float pageH = labelSize.Height;
    //    float padL = MmToPt(2.5f);
    //    float padR = MmToPt(2.5f);
    //    float topY = pageH - MmToPt(3f);

    //    // --- G-LUXE (top-left) ---
    //    cb.BeginText();
    //    cb.SetFontAndSize(bf, MmToPt(3.6f));
    //    cb.ShowTextAligned(Element.ALIGN_LEFT, "G-LUXE", padL, topY, 0);
    //    cb.EndText();

    //    // --- PO Number (top-right) ---
    //    cb.BeginText();
    //    cb.SetFontAndSize(bf, MmToPt(2.8f));
    //    cb.ShowTextAligned(Element.ALIGN_RIGHT, $"PO#{poNumber}", pageW - padR, topY, 0);
    //    cb.EndText();

    //    // --- Date (below PO) ---
    //    cb.BeginText();
    //    cb.SetFontAndSize(bf, MmToPt(2.8f));
    //    cb.ShowTextAligned(Element.ALIGN_RIGHT, date, pageW - padR, topY - MmToPt(3.4f), 0);
    //    cb.EndText();

    //    // --- Product Code (below G-LUXE) ---
    //    float codeY = topY - MmToPt(4.5f);
    //    cb.BeginText();
    //    cb.SetFontAndSize(bf, MmToPt(3.0f));
    //    cb.ShowTextAligned(Element.ALIGN_LEFT, code, padL, codeY, 0);
    //    cb.EndText();

    //    // --- Barcode generation ---
    //    Barcode128 barcode = new Barcode128
    //    {
    //      CodeType = Barcode.CODE128,
    //      Code = fullEan,
    //      BarHeight = MmToPt(12f),
    //      X = MmToPt(0.27f)
    //    };

    //    Image barcodeImg = barcode.CreateImageWithBarcode(cb, null, null);
    //    float availW = pageW - padL - padR;
    //    float scale = availW / barcodeImg.Width;
    //    if (scale > 1f) scale = 1f;

    //    float bcW = barcodeImg.Width * scale;
    //    float bcH = barcodeImg.Height * scale;
    //    float bcX = (pageW - bcW) / 2f;
    //    float bcY = MmToPt(1.8f);

    //    barcodeImg.ScaleAbsolute(bcW, bcH);
    //    barcodeImg.SetAbsolutePosition(bcX, bcY);
    //    cb.AddImage(barcodeImg);

    //    // --- Barcode text (below bars) ---

    //  }

    //  // --- Close document and return ---
    //  doc.Close();
    //  return File(ms.ToArray(), "application/pdf", $"Labels_{startSerial}-{endSerial}.pdf");
    //}

    [HttpGet("generate-labels")]
    public IActionResult GenerateLabels(
    [FromQuery] string poNumber = "13100/FM251204",
    [FromQuery] string eanCode = "027212250",
    [FromQuery] string date = "12/25",
    [FromQuery] string code ="",
    [FromQuery] int startSerial = 601,
    [FromQuery] int endSerial = 1000)
    {
      // --- Setup memory and document ---
      using MemoryStream ms = new MemoryStream();
      float MmToPt(float mm) => mm * 72f / 25.4f;
      Rectangle labelSize = new Rectangle(MmToPt(70f), MmToPt(25f));
      Document doc = new Document(labelSize, 0, 0, 0, 0);
      PdfWriter writer = PdfWriter.GetInstance(doc, ms);
      doc.Open();
      BaseFont bf = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, false);
      PdfContentByte cb = writer.DirectContent;

      // --- Loop through serial numbers ---
      for (int serial = startSerial; serial <= endSerial; serial++)
      {
        // Parse date to get MM and YY
        string[] dateParts = date.Split('/');
        string mm = dateParts.Length > 0 ? dateParts[0].PadLeft(2, '0') : "00";
        string yy = dateParts.Length > 1 ? dateParts[1].PadLeft(2, '0') : "00";
        string serialStr = serial.ToString("D5");

        // Build barcode: code (without dashes) + MMYY + SerialNo
        // Example: 5020GLXUSCHA + 1224 + 00601 = 5020GLXUSCHA1224000601
        string barcodeValue = $"{code}{mm}{yy}{serialStr}";

        if (serial > startSerial)
          doc.NewPage();

        float pageW = labelSize.Width;
        float pageH = labelSize.Height;
        float padL = MmToPt(2.5f);
        float padR = MmToPt(2.5f);
        float topY = pageH - MmToPt(3f);

        // --- G-LUXE (top-left) ---
        cb.BeginText();
        cb.SetFontAndSize(bf, MmToPt(3.6f));
        cb.ShowTextAligned(Element.ALIGN_LEFT, "G-LUXE", padL, topY, 0);
        cb.EndText();

        // --- PO Number (top-right) ---
        cb.BeginText();
        cb.SetFontAndSize(bf, MmToPt(2.8f));
        cb.ShowTextAligned(Element.ALIGN_RIGHT, $"PO#{poNumber}", pageW - padR, topY, 0);
        cb.EndText();

        // --- Date (below PO) ---
        cb.BeginText();
        cb.SetFontAndSize(bf, MmToPt(2.8f));
        cb.ShowTextAligned(Element.ALIGN_RIGHT, date, pageW - padR, topY - MmToPt(3.4f), 0);
        cb.EndText();

        // --- EAN Code with dashes (below G-LUXE) ---
        // Display the eanCode parameter which has format: Product-ProductCode-Dest-Model
        float codeY = topY - MmToPt(4.5f);
        cb.BeginText();
        cb.SetFontAndSize(bf, MmToPt(3.0f));
        cb.ShowTextAligned(Element.ALIGN_LEFT, eanCode, padL, codeY, 0);
        cb.EndText();

        // --- Barcode generation ---
        Barcode128 barcode = new Barcode128
        {
          CodeType = Barcode.CODE128,
          Code = barcodeValue,  // Use the constructed barcode value
          BarHeight = MmToPt(12f),
          X = MmToPt(0.27f)
        };
        Image barcodeImg = barcode.CreateImageWithBarcode(cb, null, null);
        float availW = pageW - padL - padR;
        float scale = availW / barcodeImg.Width;
        if (scale > 1f) scale = 1f;
        float bcW = barcodeImg.Width * scale;
        float bcH = barcodeImg.Height * scale;
        float bcX = (pageW - bcW) / 2f;
        float bcY = MmToPt(1.8f);
        barcodeImg.ScaleAbsolute(bcW, bcH);
        barcodeImg.SetAbsolutePosition(bcX, bcY);
        cb.AddImage(barcodeImg);
      }

      // --- Close document and return ---
      doc.Close();
      return File(ms.ToArray(), "application/pdf", $"Labels_{startSerial}-{endSerial}.pdf");
    }
    [HttpPost("generate-label")]
    public IActionResult GenerateLabel([FromBody] ProductLabelRequest request)
    {
      if (request == null || string.IsNullOrEmpty(request.StickerName))
        return BadRequest("A valid request with a StickerName is required.");

      if (request.ProductLabelData == null && request.ProductLabel1Data == null &&
          request.ProductLabel2Data == null && request.MedicalDeviceLabelData == null &&
          request.ProductLabel3Data == null && request.ProductLabelArabicData == null)
        return BadRequest("You must provide data for one of the label types.");

      try
      {
        string templatePath = Path.Combine(_hostingEnvironment.ContentRootPath, "Templates", $"{request.StickerName}.docx");
        if (!System.IO.File.Exists(templatePath))
          return NotFound($"Template file not found: {request.StickerName}.docx");

        using WordDocument mergedDocument = new WordDocument();
        bool isFirstPage = true;
        bool skipAlternatePages = false;

        // ProductLabelData
        if (request.ProductLabelData != null)
        {
          skipAlternatePages = true;
          var data = request.ProductLabelData;
          float? width = data.LabelWidth;
          float? height = data.LabelHeight;

          bool isBatchMode = !string.IsNullOrEmpty(data.StartingSerialNo) && !string.IsNullOrEmpty(data.EndingSerialNo);
          if (isBatchMode)
          {
            var (prefix, startNum, endNum, errorMessage) = ParseSerialNumberRange(data.StartingSerialNo, data.EndingSerialNo);
            if (!string.IsNullOrEmpty(errorMessage))
              return BadRequest(errorMessage);

            for (int i = startNum; i <= endNum; i++)
            {
              var currentData = new ProductLabelData
              {
                ItemNo = data.ItemNo,
                Description = data.Description,
                Quantity = data.Quantity,
                ExtendedText = data.ExtendedText,
                Ean13No = data.Ean128No,
                Ean128No = "(01)" + data.Ean128No + "(21)" + prefix + i.ToString(),
                SerialNo = prefix + i.ToString()
              };
              ProcessAndMerge(mergedDocument, templatePath, width, height, currentData, ref isFirstPage);
            }
          }
          else
          {
            ProcessAndMerge(mergedDocument, templatePath, width, height, data, ref isFirstPage);
          }
        }
        // ProductLabel1Data
        else if (request.ProductLabel1Data != null)
        {
          var data = request.ProductLabel1Data;

          var (prefix, startNum, endNum, errorMessage) = ParseSerialNumberRange(data.StartingSerialNo, data.EndingSerialNo);
          if (!string.IsNullOrEmpty(errorMessage))
            return BadRequest(errorMessage);

          for (int i = startNum; i <= endNum; i++)
          {

            string currentSerial =  prefix + i.ToString();
            ProcessAndMerge(mergedDocument, templatePath, data.LabelWidth, data.LabelHeight, data, currentSerial, ref isFirstPage);
          }
        }
        // ProductLabelArabicData
        else if (request.ProductLabelArabicData != null)
        {
          var data = request.ProductLabelArabicData;
          var (prefix, startNum, endNum, errorMessage) = ParseSerialNumberRange(data.StartingSerialNo, data.EndingSerialNo);
          if (!string.IsNullOrEmpty(errorMessage))
            return BadRequest(errorMessage);

          for (int i = startNum; i <= endNum; i++)
          {
            var currentData = new ProductLabelArabicData
            {
              Description = data.Description,
              Ean128No = data.Ean128No,
              Date = data.Date,
              Weight = data.Weight,
              LabelWidth = data.LabelWidth,
              LabelHeight = data.LabelHeight,
              SerialNo = prefix + i.ToString()
            };
            ProcessAndMerge(mergedDocument, templatePath, data.LabelWidth, data.LabelHeight, currentData, ref isFirstPage);
          }
        }
        // ProductLabel2Data
        else if (request.ProductLabel2Data != null)
        {
          var data = request.ProductLabel2Data;
          var (prefix, startNum, endNum, errorMessage) = ParseSerialNumberRange(data.StartingSerialNo, data.EndingSerialNo);
          if (!string.IsNullOrEmpty(errorMessage))
            return BadRequest(errorMessage);

          for (int i = startNum; i <= endNum; i++)
          {
            string currentSerial = prefix + i.ToString();
            ProcessAndMerge(mergedDocument, templatePath, data.LabelWidth, data.LabelHeight, data, currentSerial, ref isFirstPage);
          }
        }
        // MedicalDeviceLabelData
        else if (request.MedicalDeviceLabelData != null)
        {
          var data = request.MedicalDeviceLabelData;
          var (prefix, startNum, endNum, errorMessage) = ParseSerialNumberRange(data.StartingSerialNo, data.EndingSerialNo);
          if (!string.IsNullOrEmpty(errorMessage))
            return BadRequest(errorMessage);

          for (long i = startNum; i <= endNum; i++)
          {
            string currentSerial = prefix + i.ToString();
            ProcessAndMerge(mergedDocument, templatePath, data.LabelWidth, data.LabelHeight, data, currentSerial, ref isFirstPage);
          }
        }
        // ProductLabel3Data
        else if (request.ProductLabel3Data != null)
        {
          var data = request.ProductLabel3Data;
          float? width = data.LabelWidth;
          float? height = data.LabelHeight;

          bool isBatchMode = !string.IsNullOrEmpty(data.StartingSerialNo) && !string.IsNullOrEmpty(data.EndingSerialNo);
          if (isBatchMode)
          {
            var (prefix, startNum, endNum, errorMessage) = ParseSerialNumberRange(data.StartingSerialNo, data.EndingSerialNo);
            if (!string.IsNullOrEmpty(errorMessage))
              return BadRequest(errorMessage);

            for (int i = startNum; i <= endNum; i++)
            {
              var currentData = new ProductLabel3Data
              {
                ItemNo = data.ItemNo,
                Description = data.Description,
                Ean128No = data.Ean128No,
                Date = data.Date,
                LabelWidth = data.LabelWidth,
                LabelHeight = data.LabelHeight,
                SerialNo = prefix + i.ToString()
              };
              ProcessAndMerge(mergedDocument, templatePath, width, height, currentData, ref isFirstPage);
            }
          }
          else
          {
            ProcessAndMerge(mergedDocument, templatePath, width, height, data, ref isFirstPage);
          }
        }

        return ConvertToPdf(mergedDocument, "Generated_Labels.pdf", skipAlternatePages);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "An error occurred during document generation.");
        return StatusCode(500, $"An error occurred: {ex.Message}");
      }
    }

    // Helper method to parse serial number ranges with flexible prefix handling
    private (string prefix, int startNum, int endNum, string errorMessage) ParseSerialNumberRange(string startSerial, string endSerial)
    {
      if (string.IsNullOrEmpty(startSerial) || string.IsNullOrEmpty(endSerial))
        return ("", 0, 0, "Starting and ending serial numbers are required.");

      // Extract numeric part from the end of startSerial
      int startNumLength = 0;
      for (int i = startSerial.Length - 1; i >= 0; i--)
      {
        if (char.IsDigit(startSerial[i]))
          startNumLength++;
        else
          break;
      }

      if (startNumLength == 0)
        return ("", 0, 0, $"Starting serial number '{startSerial}' does not contain numeric digits.");

      string prefix = startSerial.Substring(0, startSerial.Length - startNumLength);
      string startNumStr = startSerial.Substring(startSerial.Length - startNumLength);

      // Extract numeric part from the end of endSerial
      int endNumLength = 0;
      for (int i = endSerial.Length - 1; i >= 0; i--)
      {
        if (char.IsDigit(endSerial[i]))
          endNumLength++;
        else
          break;
      }

      if (endNumLength == 0)
        return ("", 0, 0, $"Ending serial number '{endSerial}' does not contain numeric digits.");

      string endPrefix = endSerial.Substring(0, endSerial.Length - endNumLength);
      string endNumStr = endSerial.Substring(endSerial.Length - endNumLength);

      // Validate that prefixes match
      if (prefix != endPrefix)
        return ("", 0, 0, $"Serial number prefixes do not match: '{prefix}' vs '{endPrefix}'");

      if (!int.TryParse(startNumStr, out int startNum))
        return ("", 0, 0, $"Invalid numeric part in starting serial: '{startNumStr}'");

      if (!int.TryParse(endNumStr, out int endNum))
        return ("", 0, 0, $"Invalid numeric part in ending serial: '{endNumStr}'");

      if (startNum > endNum)
        return ("", 0, 0, $"Starting serial number ({startNum}) cannot be greater than ending serial number ({endNum}).");

      return (prefix, startNum, endNum, null);
    }

    private void ProcessAndMerge(WordDocument mergedDocument, string templatePath, float? width, float? height, ProductLabelData data, ref bool isFirstPage)
    {
      if (!isFirstPage) mergedDocument.LastSection.AddParagraph().AppendBreak(BreakType.PageBreak);
      using (var singleDoc = new WordDocument(templatePath, FormatType.Docx))
      {
        if (width.HasValue && height.HasValue) SetPageSize(singleDoc, width.Value, height.Value);
        FillLegacyTemplate(singleDoc, data);
        mergedDocument.ImportContent(singleDoc, ImportOptions.UseDestinationStyles);
      }
      isFirstPage = false;
    }

    private void ProcessAndMerge(WordDocument mergedDocument, string templatePath, float? width, float? height, ProductLabel3Data data, ref bool isFirstPage)
    {
      if (!isFirstPage) mergedDocument.LastSection.AddParagraph().AppendBreak(BreakType.PageBreak);
      using (var singleDoc = new WordDocument(templatePath, FormatType.Docx))
      {
        if (width.HasValue && height.HasValue) SetPageSize(singleDoc, width.Value, height.Value);
        FillProductLabel3Template(singleDoc, data);
        mergedDocument.ImportContent(singleDoc, ImportOptions.UseDestinationStyles);
      }
      isFirstPage = false;
    }

    private void ProcessAndMerge(WordDocument mergedDocument, string templatePath, float? width, float? height, ProductLabelArabicData data, ref bool isFirstPage)
    {
      if (!isFirstPage) mergedDocument.LastSection.AddParagraph().AppendBreak(BreakType.PageBreak);
      using (var singleDoc = new WordDocument(templatePath, FormatType.Docx))
      {
        if (width.HasValue && height.HasValue) SetPageSize(singleDoc, width.Value, height.Value);
        FillProductLabelArabicTemplate(singleDoc, data);
        mergedDocument.ImportContent(singleDoc, ImportOptions.UseDestinationStyles);
      }
      isFirstPage = false;
    }

    private void ProcessAndMerge(WordDocument mergedDocument, string templatePath, float? width, float? height, ProductLabel1 data, string currentSerial, ref bool isFirstPage)
    {
      if (!isFirstPage) mergedDocument.LastSection.AddParagraph().AppendBreak(BreakType.PageBreak);
      using (var singleDoc = new WordDocument(templatePath, FormatType.Docx))
      {
        if (width.HasValue && height.HasValue) SetPageSize(singleDoc, width.Value, height.Value);
        FillNewTemplate(singleDoc, data, currentSerial);
        mergedDocument.ImportContent(singleDoc, ImportOptions.UseDestinationStyles);
      }
      isFirstPage = false;
    }

    private void FillProductLabel3Template(WordDocument document, ProductLabel3Data data)
    {
      document.Replace("{{Item_no}}", data.ItemNo ?? "", false, false);
      document.Replace("{{Serial_No}}", data.SerialNo ?? "", false, false);
      document.Replace("{{Description}}", data.Description ?? "", false, false);
      document.Replace("{{ean128_no}}", data.Ean128No ?? "", false, false);
      string date = string.IsNullOrEmpty(data.Date) ? DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd") : data.Date;
      document.Replace("{{date}}", date, false, false);

      if (!string.IsNullOrEmpty(data.Description))
      {
        string imageName = $"{data.Description}.bmp";
        string imageFilePath = Path.Combine(_dynamicImagesPath, imageName);

        if (System.IO.File.Exists(imageFilePath))
        {
          using (FileStream imageStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
          {
            using (MemoryStream ms = new MemoryStream())
            {
              imageStream.CopyTo(ms);
              ms.Position = 0;

              TextSelection[] imageSelections = document.FindAll("{{description_image}}", false, false);
              if (imageSelections != null && imageSelections.Length > 0)
              {
                foreach (TextSelection selection in imageSelections)
                {
                  WTextRange range = selection.GetAsOneRange();
                  WParagraph paragraph = range.OwnerParagraph;
                  int index = paragraph.ChildEntities.IndexOf(range);

                  WPicture picture = new WPicture(document);
                  picture.LoadImage(ms);

                  if (picture.Width > 170)
                  {
                    picture.Height = (picture.Height * 170) / picture.Width;
                    picture.Width = 170;
                  }
                  if (picture.Height > 100)
                  {
                    picture.Width = (picture.Width * 100) / picture.Height;
                    picture.Height = 100;
                  }

                  paragraph.ChildEntities.Insert(index, picture);
                  paragraph.ChildEntities.Remove(range);
                  _logger.LogInformation($"Inserted image for description: {data.Description}");
                }
              }
              else
              {
                _logger.LogWarning($"Placeholder {{{{description_image}}}} not found in template for {data.Description}.");
              }
            }
          }
        }
        else
        {
          _logger.LogWarning($"Description image not found: {imageFilePath}");
          document.Replace("{{description_image}}", "", false, false);
        }
      }
      else
      {
        document.Replace("{{description_image}}", "", false, false);
      }

      ReplaceBarcodesInDocument(document, BarcodeHelper.GenerateBarcodes(data.Ean128No, _logger),
        barcodeWidth: 120f, barcodeHeight: 25f);
    }
    private int CalculateLevenshteinDistance(string source, string target)
    {
      if (string.IsNullOrEmpty(source))
        return string.IsNullOrEmpty(target) ? 0 : target.Length;

      if (string.IsNullOrEmpty(target))
        return source.Length;

      int[,] distance = new int[source.Length + 1, target.Length + 1];

      for (int i = 0; i <= source.Length; i++)
        distance[i, 0] = i;

      for (int j = 0; j <= target.Length; j++)
        distance[0, j] = j;

      for (int i = 1; i <= source.Length; i++)
      {
        for (int j = 1; j <= target.Length; j++)
        {
          int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
          distance[i, j] = Math.Min(
            Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
            distance[i - 1, j - 1] + cost);
        }
      }

      return distance[source.Length, target.Length];
    }

    private void ProcessAndMerge(WordDocument mergedDocument, string templatePath, float? width, float? height, ProductLabel2 data, string currentSerial, ref bool isFirstPage)
    {
      if (!isFirstPage) mergedDocument.LastSection.AddParagraph().AppendBreak(BreakType.PageBreak);
      using (var singleDoc = new WordDocument(templatePath, FormatType.Docx))
      {
        if (width.HasValue && height.HasValue) SetPageSize(singleDoc, width.Value, height.Value);
        FillNewTemplate2(singleDoc, data, currentSerial);
        mergedDocument.ImportContent(singleDoc, ImportOptions.UseDestinationStyles);
      }
      isFirstPage = false;
    }

    private void FillProductLabelArabicTemplate(WordDocument document, ProductLabelArabicData data)
    {
      string date = string.IsNullOrEmpty(data.Date) ? DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd") : data.Date;

      string englishDescription = data.Description ?? "";
      string arabicDescription = GetArabicDescription(englishDescription);
      string fullDescription = $"{englishDescription}\n{arabicDescription}";

      // Format display text WITH brackets: (01)EAN128No(21)SerialNo
      string displayText = $"(01){data.Ean128No ?? ""}(21){data.SerialNo ?? ""}";

      document.Replace("{{Description}}", fullDescription, false, false);
      document.Replace("{{date}}", date, false, false);
      document.Replace("{{weight}}", data.Weight ?? "", false, false);
      document.Replace("{{Serial_No}}", data.SerialNo ?? "", false, false);
      document.Replace("{{ean128_no}}", displayText, false, false);  // Display with brackets
      document.Replace("{{Item_no}}", "", false, false);
      document.Replace("{{Width}}", "", false, false);

      // Handle description image
      if (!string.IsNullOrEmpty(data.Description))
      {
        string[] allImages = Directory.GetFiles(_dynamicImagesPath, "*.bmp");

        // Find closest matching image using Levenshtein distance or similarity score
        string imageFilePath = null;
        int bestMatchScore = int.MaxValue;

        foreach (string imagePath in allImages)
        {
          string imageFileName = Path.GetFileNameWithoutExtension(imagePath);

          // Calculate similarity (lower is better)
          int distance = CalculateLevenshteinDistance(
            data.Description.ToLower(),
            imageFileName.ToLower()
          );

          // Also check if one contains the other (bonus points)
          bool containsMatch = imageFileName.Contains(data.Description, StringComparison.OrdinalIgnoreCase) ||
                              data.Description.Contains(imageFileName, StringComparison.OrdinalIgnoreCase);

          if (containsMatch)
          {
            distance -= 100; // Heavy bonus for containment match
          }

          if (distance < bestMatchScore)
          {
            bestMatchScore = distance;
            imageFilePath = imagePath;
          }
        }

        if (!string.IsNullOrEmpty(imageFilePath) && System.IO.File.Exists(imageFilePath))
        {
          _logger.LogInformation($"Found closest matching image: {Path.GetFileName(imageFilePath)} " +
            $"(score: {bestMatchScore}) for description: {data.Description}");

          using (FileStream imageStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
          {
            using (MemoryStream ms = new MemoryStream())
            {
              imageStream.CopyTo(ms);
              ms.Position = 0;

              TextSelection[] imageSelections = document.FindAll("{{description_image}}", false, false);
              if (imageSelections != null && imageSelections.Length > 0)
              {
                foreach (TextSelection selection in imageSelections)
                {
                  WTextRange range = selection.GetAsOneRange();
                  WParagraph paragraph = range.OwnerParagraph;
                  int index = paragraph.ChildEntities.IndexOf(range);

                  WPicture picture = new WPicture(document);
                  picture.LoadImage(ms);

                  float maxWidth = 50f;
                  float maxHeight = 10f;

                  if (picture.Width > maxWidth)
                  {
                    picture.Height = (picture.Height * maxWidth) / picture.Width;
                    picture.Width = maxWidth;
                  }
                  if (picture.Height > maxHeight)
                  {
                    picture.Width = (picture.Width * maxHeight) / picture.Height;
                    picture.Height = maxHeight;
                  }

                  paragraph.ChildEntities.Insert(index, picture);
                  paragraph.ChildEntities.Remove(range);
                  _logger.LogInformation($"Successfully inserted image for description: {data.Description}");
                }
              }
              else
              {
                _logger.LogWarning($"Placeholder {{{{description_image}}}} not found in template for {data.Description}.");
              }
            }
          }
        }
        else
        {
          _logger.LogWarning($"No image file found matching description: {data.Description}");
          document.Replace("{{description_image}}", "", false, false);
        }
      }
      else
      {
        document.Replace("{{description_image}}", "", false, false);
      }

      // ----- Prepare barcode content WITHOUT brackets for barcode generation -----
      // Format: 01 + EAN128No + 21 + SerialNo (GS1-128 format)
      string barcodeContent = $"01{data.Ean128No ?? ""}21{data.SerialNo ?? ""}";

      ReplaceBarcodesInDocument(
        document,
        BarcodeHelper.GenerateBarcodes(barcodeContent, _logger),
        barcodeWidth: 120f,
        barcodeHeight: 25f);
    }
    private string GetArabicDescription(string description)
    {
      var translations = new Dictionary<string, string>
      {
        { "Action 2NG", "أكشن 2 إن جي" }
      };

      if (!string.IsNullOrEmpty(description) && translations.ContainsKey(description))
      {
        return translations[description];
      }

      return "";
    }

    private void ProcessAndMerge(WordDocument mergedDocument, string templatePath, float? width, float? height, MedicalDeviceLabel data, string currentSerial, ref bool isFirstPage)
    {
      if (!isFirstPage) mergedDocument.LastSection.AddParagraph().AppendBreak(BreakType.PageBreak);
      using (var singleDoc = new WordDocument(templatePath, FormatType.Docx))
      {
        if (width.HasValue && height.HasValue) SetPageSize(singleDoc, width.Value, height.Value);
        FillMedicalDeviceTemplate(singleDoc, data, currentSerial);
        mergedDocument.ImportContent(singleDoc, ImportOptions.UseDestinationStyles);
      }
      isFirstPage = false;
    }

    private void FillLegacyTemplate(WordDocument document, ProductLabelData data)
    {
      document.Replace("{{item_no}}", data.ItemNo ?? "", false, false);
      document.Replace("{{serial_no}}", data.SerialNo ?? "", false, false);
      document.Replace("{{quantity}}", data.Quantity ?? "", false, false);
      document.Replace("{{description}}", data.Description ?? "", false, false);
      document.Replace("{{extended_text}}", data.ExtendedText ?? "", false, false);
      document.Replace("{{ean13_no}}", data.Ean13No ?? "", false, false);
      document.Replace("{{ean128_no}}", data.Ean128No ?? "", false, false);  // Display with brackets

      // Remove brackets for barcode generation
      var dataForBarcode = new ProductLabelData
      {
        ItemNo = data.ItemNo,
        SerialNo = data.SerialNo,
        Quantity = data.Quantity,
        Ean13No = data.Ean13No,
        Ean128No = (data.Ean128No ?? "").Replace("(", "").Replace(")", "")  // Remove brackets
      };

      var allBarcodes = BarcodeHelper.GenerateBarcodes(dataForBarcode, _logger);

      var quantityBarcode = allBarcodes
        .Where(kvp => kvp.Key == "quantity_barcode")
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

      var ean128Barcode = allBarcodes
        .Where(kvp => kvp.Key == "ean128_barcode")
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

      var otherBarcodes = allBarcodes
        .Where(kvp => kvp.Key != "quantity_barcode" && kvp.Key != "ean128_barcode")
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

      ReplaceBarcodesInDocument(document, quantityBarcode, 50f, 20f);
      ReplaceBarcodesInDocument(document, ean128Barcode, 120f, 20f);  // Reduced size
      ReplaceBarcodesInDocument(document, otherBarcodes, null, null);
    }
    private void FillMedicalDeviceTemplate(WordDocument document, MedicalDeviceLabel data, string currentSerial)
    {
      document.Replace("{{REF_NO}}", data.RefNo ?? "", false, false);
      document.Replace("{{SN_NO}}", currentSerial, false, false);
      document.Replace("{{DATE}}", data.Date ?? "", false, false);
      document.Replace("{{Type_no}}", data.TypeNo ?? "", false, false);
      document.Replace("{{Weight1}}", data.Weight1 ?? "130 Kg", false, false);
      document.Replace("{{Weight2}}", data.Weight2 ?? "150 Kg", false, false);

      string trimmedSerial = currentSerial; // Start with the original value

      if (!string.IsNullOrWhiteSpace(trimmedSerial))
      {
        int slashIndex = trimmedSerial.IndexOf('/');

        // Check if slash was found AND it's not the last character
        if (slashIndex >= 0 && slashIndex < trimmedSerial.Length - 1)
        {
          // Get the part *after* the slash
          trimmedSerial = trimmedSerial.Substring(slashIndex + 1);
        }
      }
      // --- END: Serial Number Trimming Logic ---

      // Use the new trimmedSerial to build the display text
      string displayText = $"{data.BarcodeData ?? ""}{trimmedSerial}";
      document.Replace("{{barcode_number}}", displayText, false, false);
      ReplaceBarcodesInDocument(document,
        BarcodeHelper.GenerateBarcodes(data, currentSerial, _logger),
        barcodeWidth: 100f,
        barcodeHeight: 5f);
    }

    private void FillNewTemplate(WordDocument document, ProductLabel1 data, string currentSerial)
    {
      string shortDescription = string.Join(" ", data.Description?.Split(' ').Take(2) ?? Array.Empty<string>());
      string date = string.IsNullOrEmpty(data.Date) ? DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd") : data.Date;

      // Format display text WITH brackets: (01)EAN128No(21)SerialNo
      string displayText = $"(01){data.Ean128No ?? ""}(21){currentSerial}";

      // ----- Display values in document -----
      document.Replace("{{Item_no}}", data.ItemNo ?? "", false, false);
      document.Replace("{{Serial_No}}", currentSerial, false, false);
      document.Replace("{{Description}}", shortDescription, false, false);
      document.Replace("{{ean128_no}}", displayText, false, false);  // Display with brackets
      document.Replace("{{date}}", date, false, false);
      document.Replace("{{Width}}", data.Width ?? "", false, false);
      document.Replace("{{weight}}", data.Weight ?? "", false, false);

      // ----- Prepare barcode content WITHOUT brackets for barcode generation -----
      // Format: 01 + EAN128No + 21 + SerialNo (GS1-128 format)
      string barcodeContent = $"01{data.Ean128No ?? ""}21{currentSerial}";

      // Generate and replace barcodes WITH CUSTOM SIZE
      ReplaceBarcodesInDocument(
        document,
        BarcodeHelper.GenerateBarcodes(barcodeContent, _logger),
        barcodeWidth: 140f,   // ⚙️ ADJUST WIDTH HERE (in points)
        barcodeHeight: 10f    // ⚙️ ADJUST HEIGHT HERE (in points)
      );
    }
    private void FillNewTemplate2(WordDocument document, ProductLabel2 data, string currentSerial)
    {
      string date = string.IsNullOrEmpty(data.Date) ? DateTime.UtcNow.AddMinutes(330).ToString("dd-MM-yyyy") : data.Date;

      document.Replace("{{Item_no}}", data.ItemNo ?? "", false, false);
      document.Replace("{{date}}", date, false, false);
      
      document.Replace("{{ean128_no}}", data.Ean128No + currentSerial ?? "", false, false);
      document.Replace("{{po_no}}", data.PoNo ?? "", false, false);
     

      // Generate all barcodes
      var allBarcodes = BarcodeHelper.GenerateBarcodes(data, currentSerial, _logger);

      // Separate ean128 barcode for reduced size
      var ean128Barcode = allBarcodes
          .Where(kvp => kvp.Key == "ean128_barcode")
          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

      // Keep other barcodes at default size (if any are added in future)
      var otherBarcodes = allBarcodes
          .Where(kvp => kvp.Key != "ean128_barcode")
          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

      // Apply reduced size to ean128 barcode
      ReplaceBarcodesInDocument(document, ean128Barcode, barcodeWidth: 100f, barcodeHeight: 100f);

      // Apply default size to other barcodes
      ReplaceBarcodesInDocument(document, otherBarcodes, barcodeWidth: null, barcodeHeight: null);
    }
    private void ReplaceBarcodesInDocument(WordDocument document, Dictionary<string, Stream> barcodeImages, float? barcodeWidth = null, float? barcodeHeight = null)
    {
      try
      {
        foreach (var kvp in barcodeImages)
        {
          if (kvp.Value?.Length > 0)
          {
            TextSelection[] selections = document.FindAll($"{{{{{kvp.Key}}}}}", false, false);
            if (selections != null)
            {
              foreach (TextSelection selection in selections)
              {
                WTextRange range = selection.GetAsOneRange();
                WParagraph paragraph = range.OwnerParagraph;
                int index = paragraph.ChildEntities.IndexOf(range);
                WPicture picture = new WPicture(document);
                kvp.Value.Position = 0;
                picture.LoadImage(kvp.Value);

                if (barcodeWidth.HasValue && barcodeHeight.HasValue)
                {
                  picture.Width = barcodeWidth.Value;
                  picture.Height = barcodeHeight.Value;
                }

                paragraph.ChildEntities.Insert(index, picture);
                paragraph.ChildEntities.Remove(range);
              }
            }
          }
        }
      }
      finally
      {
        if (barcodeImages != null)
        {
          foreach (var stream in barcodeImages.Values)
          {
            stream?.Dispose();
          }
        }
      }
    }

    private void SetPageSize(WordDocument document, float widthMm, float heightMm)
    {
      float widthPoints = widthMm * 2.83465f;
      float heightPoints = heightMm * 2.83465f;

      foreach (WSection section in document.Sections)
      {
        section.PageSetup.PageSize = new SyncfusionSizeF(widthPoints, heightPoints);
        section.PageSetup.Margins.Top = 5f;
        section.PageSetup.Margins.Bottom = 5f;
        section.PageSetup.Margins.Left = 5f;
        section.PageSetup.Margins.Right = 5f;
      }
    }

    private IActionResult ConvertToPdf(WordDocument document, string fileName, bool skipAlternatePages = false)
    {
      using DocIORenderer renderer = new DocIORenderer();
      using SyncfusionPdfDocument pdfDocument = renderer.ConvertToPDF(document);
      using MemoryStream tempStream = new MemoryStream();
      pdfDocument.Save(tempStream);
      tempStream.Position = 0;

      using PdfLoadedDocument loadedDocument = new PdfLoadedDocument(tempStream);

      RemoveBlankPages(loadedDocument);

      if (skipAlternatePages)
      {
        RemoveAlternatePages(loadedDocument);
      }

      using MemoryStream pdfStream = new MemoryStream();
      loadedDocument.Save(pdfStream);
      pdfStream.Position = 0;

      return File(pdfStream.ToArray(), "application/pdf", fileName);
    }

    private void RemoveBlankPages(PdfLoadedDocument loadedDocument)
    {
      for (int i = loadedDocument.Pages.Count - 1; i >= 0; i--)
      {
        if (IsPageBlank(loadedDocument.Pages[i], i))
        {
          _logger.LogInformation($"Removing blank page at index {i}.");
          loadedDocument.Pages.RemoveAt(i);
        }
      }
    }

    private void RemoveAlternatePages(PdfLoadedDocument loadedDocument)
    {
      for (int i = loadedDocument.Pages.Count - 1; i >= 0; i--)
      {
        if (i % 2 == 1)
        {
          loadedDocument.Pages.RemoveAt(i);
        }
      }
    }

    private bool IsPageBlank(PdfPageBase page, int pageIndex)
    {
      if (page.IsBlank)
      {
        _logger.LogInformation($"Page {pageIndex + 1} is BLANK according to IsBlank property.");
        return true;
      }

      try
      {
        string extractedText = page.ExtractText() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(extractedText))
        {
          _logger.LogInformation($"Page {pageIndex + 1} confirmed BLANK by manual text check.");
          return true;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Error during blank page check for page {pageIndex + 1}. Assuming not blank.");
        return false;
      }

      _logger.LogInformation($"Page {pageIndex + 1} is NOT BLANK as it contains text.");
      return false;
    }
  }

  public static class BarcodeHelper
  {
    private enum BarcodeSymbology { Code128, Ean13 }

    #region Public Overloads

    public static Dictionary<string, Stream> GenerateBarcodes(ProductLabel1 data, string currentSerial, ILogger logger)
    {
      var barcodes = new Dictionary<string, Stream>();
      string content = (data.Ean128No ?? "") + currentSerial;
      barcodes.Add("ean128_barcode", GenerateBarcodeImageWithZXing(content, BarcodeSymbology.Code128, logger));
      return barcodes;
    }

    public static Dictionary<string, Stream> GenerateBarcodes(ProductLabel2 data, string currentSerial, ILogger logger)
    {
      var barcodes = new Dictionary<string, Stream>();
      // NOW INCLUDES SERIAL NUMBER: EAN128 + Serial
      string content = (data.Ean128No ?? "") + currentSerial;

      // Generate barcode with combined content
      barcodes.Add("ean128_barcode", GenerateSpecialBarcodeForProductLabel2(content, 80, 20, logger));
      return barcodes;
    }

    public static Dictionary<string, Stream> GenerateBarcodes(ProductLabelData data, ILogger logger)
    {
      var barcodes = new Dictionary<string, Stream>();
      barcodes.Add("item_no_barcode", GenerateBarcodeImageWithZXing(data.ItemNo, BarcodeSymbology.Code128, logger));
      barcodes.Add("serial_no_barcode", GenerateBarcodeImageWithZXing(data.SerialNo, BarcodeSymbology.Code128, logger,100,50));
      barcodes.Add("quantity_barcode", GenerateBarcodeImageWithZXing(data.Quantity, BarcodeSymbology.Code128, logger,100,5));
      barcodes.Add("ean13_barcode", GenerateBarcodeImageWithZXing(data.Ean13No, BarcodeSymbology.Ean13, logger));
      barcodes.Add("ean128_barcode", GenerateBarcodeImageWithZXing(data.Ean128No, BarcodeSymbology.Code128, logger));
      return barcodes;
    }

    public static Dictionary<string, Stream> GenerateBarcodes(MedicalDeviceLabel data, string currentSerial, ILogger logger)
    {
      var barcodes = new Dictionary<string, Stream>();

      // --- START: Serial Number Trimming Logic ---
      string trimmedSerial = currentSerial; // Start with the original value

      if (!string.IsNullOrWhiteSpace(trimmedSerial))
      {
        int slashIndex = trimmedSerial.IndexOf('/');

        // Check if slash was found AND it's not the last character
        if (slashIndex >= 0 && slashIndex < trimmedSerial.Length - 1)
        {
          // Get the part *after* the slash
          trimmedSerial = trimmedSerial.Substring(slashIndex + 1);
        }
      }
      // --- END: Serial Number Trimming Logic ---

      // Use the new trimmedSerial to build the barcode content
      string content = $"{data.BarcodeData ?? ""}{trimmedSerial}";

      barcodes.Add("barcode_image", GenerateBarcodeImageWithZXing(content, BarcodeSymbology.Code128, logger));
      return barcodes;
    }

    public static Dictionary<string, Stream> GenerateBarcodes(string ean128Content, ILogger logger)
    {
      var barcodes = new Dictionary<string, Stream>();
      if (!string.IsNullOrEmpty(ean128Content))
      {
        barcodes.Add("ean128_barcode", GenerateBarcodeImageWithZXing(ean128Content, BarcodeSymbology.Code128, logger));
      }
      return barcodes;
    }

    public static Dictionary<string, Stream> GenerateBarcodes(
  ProductLabel1 data,
  string currentSerial,
  ILogger logger,
  int width = 200,   // ⚙️ DEFAULT WIDTH (in pixels)
  int height = 50)   // ⚙️ DEFAULT HEIGHT (in pixels)
    {
      var barcodes = new Dictionary<string, Stream>();
      string content = (data.Ean128No ?? "") + currentSerial;
      barcodes.Add("ean128_barcode",
        GenerateBarcodeImageWithZXing(content, BarcodeSymbology.Code128, logger, width, height));
      return barcodes;
    }
    #endregion

    private static Stream GenerateSpecialBarcodeForProductLabel2(string content, int width, int height, ILogger logger)
    {
      if (string.IsNullOrWhiteSpace(content))
        return new MemoryStream();

      try
      {
        var writer = new ZXing.BarcodeWriterPixelData
        {
          Format = BarcodeFormat.CODE_128,
          Options = new EncodingOptions
          {
            Height = height,      // Use parameter instead of hardcoded 40
            Width = width,        // Use parameter instead of hardcoded 150
            Margin = 1,           // Reduced margin from 2 to 1
            PureBarcode = true
          }
        };

        var pixelData = writer.Write(content);
        using (var bitmap = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
        {
          var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
          var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
          try
          {
            System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmpData.Scan0, pixelData.Pixels.Length);
          }
          finally
          {
            bitmap.UnlockBits(bmpData);
          }

          var ms = new MemoryStream();
          bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
          ms.Position = 0;
          return ms;
        }
      }
      catch (Exception ex)
      {
        logger?.LogError(ex, $"ZXing failed to generate special barcode for content: '{content}'.");
        return new MemoryStream();
      }
    }
    private static Stream GenerateBarcodeImageWithZXing(
  string content,
  BarcodeSymbology symbology,
  ILogger logger,
  int width = 200,   // ⚙️ DEFAULT WIDTH
  int height = 50)   // ⚙️ DEFAULT HEIGHT
    {
      if (string.IsNullOrWhiteSpace(content))
        return new MemoryStream();
      try
      {
        if (symbology == BarcodeSymbology.Ean13)
        {
          string digitsOnly = new string(content.Where(char.IsDigit).ToArray());
          content = digitsOnly;
        }
        var writer = new ZXing.BarcodeWriterPixelData
        {
          Format = BarcodeFormat.CODE_128,
          Options = new EncodingOptions
          {
            Height = height,    // Use parameter instead of hardcoded 50
            Width = width,      // Use parameter instead of hardcoded 200
            Margin = 1,         // You can adjust margin here too
            PureBarcode = true
          }
        };
        var pixelData = writer.Write(content);
        using (var bitmap = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
        {
          var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
          var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
          try { System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmpData.Scan0, pixelData.Pixels.Length); }
          finally { bitmap.UnlockBits(bmpData); }
          var ms = new MemoryStream();
          bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
          ms.Position = 0;
          return ms;
        }
      }
      catch (Exception ex)
      {
        logger?.LogError(ex, $"ZXing failed to generate barcode for content: '{content}'.");
        return new MemoryStream();
      }
    }


    private static Stream GenerateBarcodeImageWithZXing(string content, BarcodeSymbology symbology, ILogger logger)
    {
      if (string.IsNullOrWhiteSpace(content))
        return new MemoryStream();
      try
      {
        if (symbology == BarcodeSymbology.Ean13)
        {
          string digitsOnly = new string(content.Where(char.IsDigit).ToArray());
          content = digitsOnly;
        }
        var writer = new ZXing.BarcodeWriterPixelData
        {
          Format = BarcodeFormat.CODE_128,
          Options = new EncodingOptions
          {
            Height = 50,
            Width = 200,
            Margin = 2,
            PureBarcode = true
          }
        };
        var pixelData = writer.Write(content);
        using (var bitmap = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
        {
          var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
          var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
          try { System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmpData.Scan0, pixelData.Pixels.Length); }
          finally { bitmap.UnlockBits(bmpData); }
          var ms = new MemoryStream();
          bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
          ms.Position = 0;
          return ms;
        }
      }
      catch (Exception ex)
      {
        logger?.LogError(ex, $"ZXing failed to generate barcode for content: '{content}'.");
        return new MemoryStream();
      }
    }
  }
}
