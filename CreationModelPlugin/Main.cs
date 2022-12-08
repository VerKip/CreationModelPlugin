using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitAPITrainingLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;

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


            //Вводные данные для размера домика
            double width = 10000;
            double depth = 5000;
            double sillheight = 600; //высота окон он базового уровня
            double elevation = 3000; //высота крыши

            CreateWalls(doc, level1.Id, level2.Id, width, depth);
            List<Wall> walls = GetWalls(doc);

            AddDoor(doc, level1, walls[0]);
            AddWindow(doc, level1, walls[1], sillheight);
            AddWindow(doc, level1, walls[2], sillheight);
            AddWindow(doc, level1, walls[3], sillheight);
            //AddRoof(doc, level2, walls);
            AddExtrusionRoof(doc, level2, walls, width, depth, elevation);

            return Result.Succeeded;
        }

        private void AddExtrusionRoof(Document doc, Level level2, List<Wall> walls, double width, double depth, double elevation)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Basic Roof"))
                .FirstOrDefault();

            double elevationParam = UnitUtils.ConvertToInternalUnits(elevation, UnitTypeId.Millimeters);
            double widthParam = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double depthParam = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);
            double dx = widthParam / 2;
            double dy = depthParam / 2;

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            var levelHeight = level2.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsDouble();

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(dx + dt, -dy - dt, levelHeight), new XYZ(-dx - dt, 0, levelHeight + elevationParam)));
            curveArray.Append(Line.CreateBound(new XYZ(-dx - dt, 0, levelHeight + elevationParam), new XYZ(-dx, dy + dt, levelHeight)));

            using (var t = new Transaction(doc, "Create extrusion roof"))
            {
                t.Start();
                ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, dx), new XYZ(0, dx, 0), doc.ActiveView);
                doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, -dx - dt, dx + dt);
                t.Commit();
            }
        }
        private void AddRoof(Document doc, Level level2, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Basic Roof"))
                .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;
            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dt, -dt, 0));
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));
            points.Add(new XYZ(-dt, -dt, 0));

            using (var t = new Autodesk.Revit.DB.Transaction(doc, "Create roof"))
            {
                t.Start();
                Application application = doc.Application;
                CurveArray footprint = application.Create.NewCurveArray();
                for (int i = 0; i < 4; i++)
                {
                    LocationCurve curve = walls[i].Location as LocationCurve;
                    XYZ p1 = curve.Curve.GetEndPoint(0);
                    XYZ p2 = curve.Curve.GetEndPoint(1);
                    Line line = Line.CreateBound(p1 + points[i], p2 + points[i+1]);
                    footprint.Append(line);
                }
                ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
                FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, level2,
                roofType, out footPrintToModelCurveMapping);

                foreach(ModelCurve m in footPrintToModelCurveMapping)
                {
                    footprintRoof.set_DefinesSlope(m, true);
                    footprintRoof.set_SlopeAngle(m, 0.5);
                }

                t.Commit();
            }
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
        private void AddDoor(Document doc, Level levelId, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0762 x 2032 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            using (var t = new Autodesk.Revit.DB.Transaction(doc, "Create family instance - door"))
            {
                t.Start();
                if (!doorType.IsActive)
                {
                    doorType.Activate();
                    doc.Regenerate();
                }

                doc.Create.NewFamilyInstance(point, doorType, wall, levelId, StructuralType.NonStructural);
                t.Commit();
            }

        }
        private void AddWindow(Document doc, Level levelId, Wall wall, double sillheight)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0406 x 1220 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            using (var t = new Transaction(doc, "Create family instance - window"))
            {
                t.Start();
                if (!windowType.IsActive)
                {
                    windowType.Activate();
                    doc.Regenerate();
                }

                double sillheightParam = UnitUtils.ConvertToInternalUnits(sillheight, UnitTypeId.Millimeters);
                FamilyInstance newWindow = doc.Create.NewFamilyInstance(point, windowType, wall, levelId, StructuralType.NonStructural);
                newWindow.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(sillheightParam);

                t.Commit();
            }
        }

        private static List<Level> GetLevels(Document doc)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            return listLevel;
        }
        private static List<Wall> GetWalls(Document doc)
        {
            List<Wall> listWall = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .OfType<Wall>()
                .ToList();
            return listWall;
        }
    }
}
