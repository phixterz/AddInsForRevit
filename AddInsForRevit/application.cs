using System;
using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace RevitAddin
{
    //dsd
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Путь к текущей сборке (.dll)
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                // Создание кнопки для запуска команды
                PushButtonData buttonData = new PushButtonData(
                    "FinishingCommand", // Идентификатор кнопки
                    "Отделка",          // Текст кнопки
                    assemblyPath,
                    "RevitAddin.FinishingCommand" // Полное имя класса с командой
                );

                // Получаем существующую панель "Внешние инструменты" на вкладке "Надстройки"
                RibbonPanel externalToolsPanel = GetRibbonPanel(application, "External Tools");

                // Добавляем кнопку на панель
                externalToolsPanel.AddItem(buttonData);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Ошибка при инициализации команды: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        /// <summary>
        /// Метод для получения панели "External Tools" на вкладке "Add-Ins"
        /// </summary>
        private RibbonPanel GetRibbonPanel(UIControlledApplication application, string panelName)
        {
            // Ищем панель с заданным именем на вкладке "Add-Ins"
            foreach (RibbonPanel panel in application.GetRibbonPanels("Add-Ins"))
            {
                if (panel.Name == panelName)
                    return panel;
            }

            // Если панель не найдена, создаём новую панель
            return application.CreateRibbonPanel("Add-Ins", panelName);
        }
    }
}
