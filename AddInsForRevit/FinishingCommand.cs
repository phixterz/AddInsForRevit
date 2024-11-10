using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using System;
using AddInsForRevit;

namespace RevitAddin
{
    [Transaction(TransactionMode.Manual)]
    public class FinishingCommand : IExternalCommand
    {
        // Публичное значение делителя, используемое в расчётах
        public static double Divider = 34.5;

        // Окно настроек
        private static FinishingSettingsWindow settingsWindow;

        // ExternalEvent и обработчик
        private static ExternalEvent recalculateEvent;
        private static RecalculateHandler recalculateHandler;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Инициализация ExternalEvent и обработчика, если они ещё не созданы
            if (recalculateHandler == null)
            {
                recalculateHandler = new RecalculateHandler { CommandData = commandData };
                recalculateEvent = ExternalEvent.Create(recalculateHandler);
            }

            // Открытие окна настроек, если оно ещё не открыто
            if (settingsWindow == null || !settingsWindow.IsVisible)
            {
                settingsWindow = FinishingSettingsWindow.Instance;
                settingsWindow.RecalculateAction = () => recalculateEvent.Raise();
                settingsWindow.Show();
            }
            else
            {
                settingsWindow.Activate();
            }

            return Result.Succeeded;
        }
    }

    /// <summary>
    /// Обработчик для пересчёта, запускаемый через ExternalEvent
    /// </summary>
    public class RecalculateHandler : IExternalEventHandler
    {
        public ExternalCommandData CommandData { get; set; }

        public void Execute(UIApplication app)
        {
            if (CommandData == null) return;

            Document doc = CommandData.Application.ActiveUIDocument.Document;

            using (Transaction trans = new Transaction(doc, "Update Finishing"))
            {
                try
                {
                    trans.Start();

                    // Пример пересчёта
                    var rooms = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .ToList();

                    if (rooms.Count == 0)
                    {
                        TaskDialog.Show("Ошибка", "Помещения не найдены в проекте.");
                        trans.RollBack();
                        return;
                    }

                    // Выполнение обработки элементов
                    var floors = GetFilteredElements(doc, BuiltInCategory.OST_Floors, "тделка пол");
                    var walls = GetFilteredElements(doc, BuiltInCategory.OST_Walls, "тделка стен");
                    var ceilings = GetFilteredElements(doc, BuiltInCategory.OST_Ceilings, "тделка потолк");

                    if (floors.Count == 0 && walls.Count == 0 && ceilings.Count == 0)
                    {
                        TaskDialog.Show("Ошибка", "Элементы отделки не найдены в проекте.");
                        trans.RollBack();
                        return;
                    }

                    var dictFloors = new Dictionary<string, Dictionary<string, List<object>>>();
                    var dictWalls = new Dictionary<string, Dictionary<string, List<object>>>();
                    var dictCeilings = new Dictionary<string, Dictionary<string, List<object>>>();

                    // Заполнение словарей
                    foreach (Element element in floors)
                        dictFloors = CalculateFinishing(dictFloors, GetElementParamSet(element, doc));

                    foreach (Element element in walls)
                        dictWalls = CalculateFinishing(dictWalls, GetElementParamSet(element, doc));

                    foreach (Element element in ceilings)
                        dictCeilings = CalculateFinishing(dictCeilings, GetElementParamSet(element, doc));

                    SetFinishingInRoom(rooms, dictFloors, new[] { "SEV_Отделка полов", "SEV_Количество отделки полов" }, doc);
                    SetFinishingInRoom(rooms, dictWalls, new[] { "SEV_Отделка стен", "SEV_Количество отделки стен" }, doc);
                    SetFinishingInRoom(rooms, dictCeilings, new[] { "SEV_Отделка потолков", "SEV_Количество отделки потолков" }, doc);

                    trans.Commit();
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                    trans.RollBack();
                }
            }
        }
        private void SetFinishingInRoom(
    IList<Element> rooms,
    Dictionary<string, Dictionary<string, List<object>>> dict,
    string[] paramWalls,
    Document doc)
        {
            foreach (var room in rooms)
            {
                string roomNumber = room.LookupParameter("Номер").AsString();
                if (!dict.ContainsKey(roomNumber)) continue;

                string multistring = "";
                string multires = "";

                foreach (var mark in dict[roomNumber].Keys)
                {
                    string description = $"{mark}.\n{dict[roomNumber][mark][2]}\n";
                    int rowCount = RowCountByStrLen(description);
                    multistring += description;

                    double result = (string)dict[roomNumber][mark][3] == "м2" ?
                        Math.Round((double)dict[roomNumber][mark][0], 1) :
                        Math.Round((double)dict[roomNumber][mark][1], 1);

                    string resultStr = $"{result} {dict[roomNumber][mark][3]}";
                    for (int i = 0; i < rowCount; i++)
                    {
                        resultStr += "\n";
                    }
                    multires += resultStr + "\n";
                }

                room.LookupParameter(paramWalls[0]).Set(multistring);
                room.LookupParameter(paramWalls[1]).Set(multires);
            }
        }
        private int RowCountByStrLen(string str)
        {
            return (int)Math.Round(str.Length / FinishingCommand.Divider, 0);
        }
        public string GetName()
        {
            return "RecalculateHandler";
        }

        // Дополнительные методы для обработки
        private List<Element> GetFilteredElements(Document doc, BuiltInCategory category, string search)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .Where(e => GetParameterValue(doc.GetElement(e.GetTypeId()), "Группа модели")?.Contains(search) == true)
                .ToList();
        }

        private string GetParameterValue(Element element, string paramName)
        {
            Parameter param = element.LookupParameter(paramName);
            return param != null && param.HasValue ? param.AsString() : null;
        }

        private List<object> GetElementParamSet(Element element, Document doc)
        {
            string roomNumber = GetParameterValue(element, "SEV_Номер помещения") ?? "без номера";
            string mark = GetParameterValue(doc.GetElement(element.GetTypeId()), "ADSK_Марка") ?? "(без марки)";
            string description = GetParameterValue(doc.GetElement(element.GetTypeId()), "Описание") ?? "(без наименования)";
            string unit = GetParameterValue(element, "ADSK_Единица измерения") ?? "м2";

            double area = element.LookupParameter("Площадь") != null ? Math.Round(element.LookupParameter("Площадь").AsDouble() * 0.092903, 2) : 0.01;
            double length = element.LookupParameter("Длина") != null ? Math.Round(element.LookupParameter("Длина").AsDouble() / 1000, 2) : 0.01;

            return new List<object> { roomNumber, mark, area, length, description, unit };
        }

        private Dictionary<string, Dictionary<string, List<object>>> CalculateFinishing(
            Dictionary<string, Dictionary<string, List<object>>> dict, List<object> set)
        {
            string roomNumber = set[0].ToString();
            string mark = set[1].ToString();

            if (!dict.ContainsKey(roomNumber))
                dict[roomNumber] = new Dictionary<string, List<object>>();

            if (dict[roomNumber].ContainsKey(mark))
            {
                dict[roomNumber][mark][0] = (double)dict[roomNumber][mark][0] + (double)set[2];
                dict[roomNumber][mark][1] = (double)dict[roomNumber][mark][1] + (double)set[3];
            }
            else
            {
                dict[roomNumber][mark] = new List<object> { set[2], set[3], set[4], set[5] };
            }

            return dict;
        }
    }
}
