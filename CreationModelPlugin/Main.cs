using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [Transaction(TransactionMode.Manual)]

    public class Main : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            Level level1 = GetLevels(doc)
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();

            Level level2 = GetLevels(doc)
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault();

            CreateWalls(doc, level1.Id, level2.Id, 10000, 5000);


            return Result.Succeeded;
        }



        private static List<Level> GetLevels(Document doc)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            return listLevel;
        }

        private void CreateWalls(Document doc, ElementId levelId, ElementId elevationLevel, double width, double depth)
        {
            //Список точек, через которые будут проходить стены
            double widthParam = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double depthParam = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);
            double dx = widthParam / 2;
            double dy = depthParam / 2;
            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Wall> walls = new List<Wall>();
            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, levelId, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(elevationLevel);
            }
            transaction.Commit();
        }



    }
}
