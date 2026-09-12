namespace ticketing_system_backend.Models
{
  public class ProductLabelRequest
  {
    public string StickerName { get; set; }
    public ProductLabelData? ProductLabelData { get; set; }
    public ProductLabel1? ProductLabel1Data { get; set; }
    public ProductLabel2? ProductLabel2Data { get; set; }
    public MedicalDeviceLabel? MedicalDeviceLabelData { get; set; }
    public ProductLabel3Data? ProductLabel3Data { get; set; }
    public ProductLabelArabicData? ProductLabelArabicData { get; set; }
  }
  public class ProductLabelArabicData
  {
    // Fields from your finished label (image_7004bb.png)
    public string Date { get; set; }
    public string Description { get; set; }  // e.g., "Action 2NG"
    public string? SerialNo { get; set; }       // e.g., "25GFM014478"
    public string Ean128No { get; set; }     // e.g., "(01)03662050045751(21)25GFM014478"
    public string Weight { get; set; }         // e.g., "125 kg"

    // Fields for batching and page size
    public string StartingSerialNo { get; set; }
    public string EndingSerialNo { get; set; }
    public float? LabelWidth { get; set; }
    public float? LabelHeight { get; set; }
  }
  public class ProductLabelData
  {
    public string ItemNo { get; set; }
    public string Description { get; set; }
    public string Quantity { get; set; }
    public string ExtendedText { get; set; }
    public string Ean13No { get; set; }
    public string Ean128No { get; set; }
    public string? SerialNo { get; set; }
    public string? StartingSerialNo { get; set; }
    public string? EndingSerialNo { get; set; }

    // MOVED HERE: Size for this specific template
    public float? LabelWidth { get; set; }
    public float? LabelHeight { get; set; }
  }
  public class ProductLabel2
  {
    public string StartingSerialNo { get; set; }
    public string EndingSerialNo { get; set; }
    public string ItemNo { get; set; }
    public string Date { get; set; }
    public string Ean128No { get; set; }
    public string PoNo { get; set; } // New field from your image
    public float? LabelWidth { get; set; }
    public float? LabelHeight { get; set; }
  }
  public class MedicalDeviceLabel
  {
    public string RefNo { get; set; }
    public string? SerialNo { get; set; }
    public string Date { get; set; }
    public string TypeNo { get; set; }
    public string Weight1 { get; set; }  // Standing person weight (e.g., "130 Kg")
    public string Weight2 { get; set; }  // Wheelchair weight (e.g., "150 Kg")
    public string BarcodeData { get; set; }  // The actual barcode data to encode
    public string StartingSerialNo { get; set; }
    public string EndingSerialNo { get; set; }
    public float? LabelWidth { get; set; }
    public float? LabelHeight { get; set; }
  }
  public class ProductLabel3Data
  {
    public string ItemNo { get; set; }
    public string SerialNo { get; set; } // Added for consistency, if needed
    public string Description { get; set; } // This will be used to find the image
    public string Ean128No { get; set; }
    public string Date { get; set; }
    public float? LabelWidth { get; set; }
    public float? LabelHeight { get; set; }
    public string StartingSerialNo { get; set; } // For batch printing
    public string EndingSerialNo { get; set; } // For batch printing
  }
  public class LabelRequest
  {
    public string RefNo { get; set; }
    public string SerialNo { get; set; }
    public string ManufactureDate { get; set; }
    public string TypeNo { get; set; }
    public string BarcodeNumber { get; set; }
  }
  public class ProductLabel1
  {
    public string StartingSerialNo { get; set; }
    public string EndingSerialNo { get; set; }
    public string ItemNo { get; set; }
    public string Description { get; set; }
    public string Ean128No { get; set; }
    public string Date { get; set; }
    public string Width { get; set; }
    public string Weight { get; set; }

    public float? BarcodeWidth { get; set; }   // X dimension (bar width)
    public float? BarcodeHeight { get; set; }
    // MOVED HERE: Size for this specific template
    public float? LabelWidth { get; set; }
    public float? LabelHeight { get; set; }
  }
}
