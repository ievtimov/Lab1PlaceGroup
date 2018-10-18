using System;

using System.Collections.Generic;

using System.Linq;

using System.Text;

using System.Threading.Tasks;

using Autodesk.Revit.ApplicationServices;

using Autodesk.Revit.Attributes;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using Autodesk.Revit.UI.Selection;

namespace Lab1PlaceGroup

{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Lab6FindRoom : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Get application and documnet objects
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            try
            {
                //Define a reference Object to accept the pick result
                Reference pickedRef = null;

                //Pick a group
                Selection sel = uiapp.ActiveUIDocument.Selection;

                //pickedref = sel.PickObject(ObjectType.Element, "Please select a group");
                GroupPickFilter selFilter = new GroupPickFilter();

                pickedRef = sel.PickObject(ObjectType.Element, selFilter,
                  "Please select a group");

                Element elem = doc.GetElement(pickedRef);

                Group group = elem as Group;

                // Get the group's center point
                XYZ origin = GetElementCenter(group);

                // Get the room that the picked group is located in
                Room room = GetRoomOfGroup(doc, origin);

                // Get the room's center point
                XYZ sourceCenter = GetRoomCenter(room);
                //string coords =
                //  "X = " + sourceCenter.X.ToString() + "\r\n" +
                //  "Y = " + sourceCenter.Y.ToString() + "\r\n" +
                //  "Z = " + sourceCenter.Z.ToString();
                //TaskDialog.Show("Source room Center", coords);

                // Ask the user to pick target rooms
                RoomPickFilter roomPickFilter = new RoomPickFilter();
                IList<Reference> rooms =
                  sel.PickObjects(
                    ObjectType.Element,
                    roomPickFilter,
                    "Select target rooms for duplicate furniture group");

                ////Pick point
                //XYZ point = sel.PickPoint("Please pick a point to place group");

                //Place the group
                Transaction trans = new Transaction(doc);

                trans.Start("Lab");

                PlaceFurnitureInRooms(doc, rooms, sourceCenter, group.GroupType, origin);

                trans.Commit();

                return Result.Succeeded;
            }
            //If the user right-clicks or presses Esc, handle the exception
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            //Catch other errors
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// Filter to constrain picking to model groups. Only model groups
        /// are highlighted and can be selected when cursor is hovering.
        public class GroupPickFilter : ISelectionFilter
        {
            public bool AllowElement(Element e)
            {
                return (e.Category.Id.IntegerValue.Equals(
                     (int)BuiltInCategory.OST_IOSModelGroups));
            }

            public bool AllowReference(Reference r, XYZ p)
            {
                return false;
            }
        }

        public XYZ GetElementCenter(Element elem)
        {
            BoundingBoxXYZ bounding = elem.get_BoundingBox(null);
            XYZ center = (bounding.Max + bounding.Min) * 0.5;
            return center;
        }

        /// Return the room in which the given point is located
        Room GetRoomOfGroup(Document doc, XYZ point)
        {
            FilteredElementCollector collector =
              new FilteredElementCollector(doc);
            collector.OfCategory(BuiltInCategory.OST_Rooms);
            Room room = null;
            foreach (Element elem in collector)
            {
                room = elem as Room;
                if (room != null)
                {
                    // Decide if this point is in the picked room                  
                    if (room.IsPointInRoom(point))
                    {
                        break;
                    }
                }
            }
            return room;
        }

        /// Return a room's center point coordinates.
        /// Z value is equal to the bottom of the room
        public XYZ GetRoomCenter(Room room)
        {
            // Get the room center point.
            XYZ boundCenter = GetElementCenter(room);

            LocationPoint locPt = (LocationPoint)room.Location;

            XYZ roomCenter = new XYZ(boundCenter.X, boundCenter.Y, locPt.Point.Z);

            return roomCenter;
        }

        /// Filter to constrain picking to rooms
        public class RoomPickFilter : ISelectionFilter
        {
            public bool AllowElement(Element e)
            {
                return (e.Category.Id.IntegerValue.Equals(
                  (int)BuiltInCategory.OST_Rooms));
            }

            public bool AllowReference(Reference r, XYZ p)
            {
                return false;
            }
        }

        /// Copy the group to each of the provided rooms. The position
        /// at which the group should be placed is based on the target
        /// room's center point: it should have the same offset from
        /// this point as the original had from the center of its room
        public void PlaceFurnitureInRooms(
          Document doc,
          IList<Reference> rooms,
          XYZ sourceCenter,
          GroupType gt,
          XYZ groupOrigin)
        {
            XYZ offset = groupOrigin - sourceCenter;
            XYZ offsetXY = new XYZ(offset.X, offset.Y, 0);

            foreach (Reference r in rooms)
            {
                Room roomTarget = doc.GetElement(r) as Room;
                if (roomTarget != null)
                {
                    XYZ roomCenter = GetRoomCenter(roomTarget);
                    Group group = doc.Create.PlaceGroup(roomCenter + offsetXY, gt);
                }
            }
        }
    }
}