using System.Collections.Generic;

namespace CRM.ViewModels
{
    public class ImportMappingViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public List<string> ExcelHeaders { get; set; } = new();
        public List<List<string>> PreviewRows { get; set; } = new();
        public List<CrmFieldDefinition> CrmFields { get; set; } = new();
    }

    public class CrmFieldDefinition
    {
        public string SystemName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
    }
}