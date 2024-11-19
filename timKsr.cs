using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;

namespace timKsr
{
    [Transaction(TransactionMode.Manual)]
    public class UpdateIfcExportType : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Получение документа Revit
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Инициализация HttpClient
            HttpClient httpClient = new HttpClient();

            // Фильтр элементов с параметром "Код КСР"
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            // Начинаем транзакцию
            using (Transaction trans = new Transaction(doc, "Обновить IfcExportType"))
            {
                trans.Start();

                foreach (Element element in collector)
                {
                    // Получаем значение параметра "Код КСР"
                    Parameter ksrCodeParam = element.LookupParameter("Код КСР");
                    if (ksrCodeParam == null || !ksrCodeParam.HasValue)
                        continue;

                    string ksrCode = ksrCodeParam.AsString();
                    if (string.IsNullOrEmpty(ksrCode))
                        continue;

                    try
                    {
                        // Отправляем запрос к API
                        string apiUrl = $"https://tim-ksr.ru/ksr/{ksrCode}";
                        HttpResponseMessage response = httpClient.GetAsync(apiUrl).Result;

                        if (!response.IsSuccessStatusCode)
                        {
                            TaskDialog.Show("Ошибка API", $"Не удалось получить данные для {ksrCode}: {response.StatusCode}");
                            continue;
                        }

                        // Читаем JSON-ответ
                        string jsonResponse = response.Content.ReadAsStringAsync().Result;

                        // Попробуем обработать ответ как объект
                        KsrResponse ksrData = null;
                        List<KsrResponse> ksrDataList = null;

                        try
                        {
                            ksrData = JsonConvert.DeserializeObject<KsrResponse>(jsonResponse);
                        }
                        catch (JsonSerializationException)
                        {
                            ksrDataList = JsonConvert.DeserializeObject<List<KsrResponse>>(jsonResponse);
                        }

                        // Если данные найдены, обновляем параметры элемента
                        if (ksrData != null)
                        {
                            UpdateIfcExportTypeParam(element, ksrData);
                        }
                        else if (ksrDataList != null && ksrDataList.Count > 0)
                        {
                            UpdateIfcExportTypeParam(element, ksrDataList[0]);
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Ошибка", $"Ошибка обработки элемента {element.Id}: {ex.Message}");
                    }
                }

                trans.Commit();
            }

            return Result.Succeeded;
        }

        private void UpdateIfcExportTypeParam(Element element, KsrResponse ksrData)
        {
            Parameter ifcExportTypeParam = element.LookupParameter("IfcExportAs");
            if (ifcExportTypeParam != null && ksrData.ifc4class != null)
            {
                if (ksrData.ifc4type != null)
                {
                    ifcExportTypeParam.Set(ksrData.ifc4class + '.' + ksrData.ifc4type);
                }
                else
                {
                    ifcExportTypeParam.Set(ksrData.ifc4class);
                }
            }
        }
    }

    // Модель для десериализации JSON-ответа
    public class KsrResponse
    {
        public string ifc4class { get; set; }
        public string ifc4type { get; set; }
    }
}