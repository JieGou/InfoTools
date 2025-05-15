using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.PipeNetworkCreating
{
    public partial class PipeNetworkGraph
    {
        private readonly double ZERO_LENGTH = Tolerance.Global.EqualPoint;

        /// <summary>
        /// Distance from the well at which text labels are captured
        /// well numbers
        /// </summary>
        public const double DISTANCE_TO_GET_WELL_LBL = 4;

        /// <summary>
        /// Distance from the well where text labels are captured
        /// connections to this well
        /// </summary>
        private const double DISTANCE_TO_GET_JUNCTION_LBLS = 3;

        /// <summary>
        /// If the text is located at a smaller distance to the network node, then it will definitely be
        /// considered as one of the competing signature options for this node
        /// </summary>
        private const double WELL_LBL_COMPATITORS_DISTANCE = 3;

        /// <summary>
        /// If the signature of the well connection is located further from the connection
        /// than this distance, then consider that this signature as a rule
        /// cannot refer to this connection
        /// </summary>
        private const double LBL_TOO_FAR_FROM_LINE = 2;

        /// <summary>
        /// If the polyline is longer, the position of the text along the polyline will affect the
        /// priority of text binding to the attachment
        /// </summary>
        private const double EDGE_LENGTH_LBL_LONGITUDINAL_POSITION_MATTERS = 1.5;

        /// <summary>
        /// If the text is located at a smaller distance from the polyline, then it will definitely be
        /// considered as one of the competing variants of the signatures of this polyline
        /// </summary>
        private const double JUNCTION_LBL_COMPATITORS_DISTANCE = 0.2;

        /// <summary>
        /// Marker layer
        /// </summary>
        private const string MARKER_LAYER = "S1NF0_Markers";

        private const int WELL_MARKER_COLOR_INDEX = 230;

        private const int SQUARES_WITH_NO_DATA_COLOR_INDEX = 30;

        private const int NODE_WARNING_COLOR_INDEX = 50;

        private const int LBL_DULICATE_COLOR_INDEX = 20;

        private const string DATA_MATCHING_MESSAGE = "m";

        private const string SQUARE_WITH_NO_DATA_MESSAGE = "No Excel file";

        private const string LBL_DULICATE_MESSAGE = "One signature is linked to multiple objects";

        private const double BLOCK_NEAR_POLYLINE_DISTANCE = 0.2;

        private const string BLOCK_NEAR_POLYLINE_NOT_ON_ENDPOINT_MESSAGE = "The block is very close to the network line but not at one of the end points";

        private const int BLOCK_NEAR_POLYLINE_NOT_ON_ENDPOINT_COLOR_INDEX = 1;

        private enum NodeWarnings
        {
            Null = 0,
            WellLblNotFound = 1,//Well signature not found
            JunctionLblsNotFound = 2,//combination of signatures of accessions not found
            AttachmentCountNotMatches = 4,//number of joins does not match excel
            TShapedIntersection = 8,//t-intersection

        }

        private readonly Dictionary<NodeWarnings, string> nodeWarningsMessages = new Dictionary<NodeWarnings, string>()
        {
            { NodeWarnings.WellLblNotFound, "Well signature not found" },
            { NodeWarnings.JunctionLblsNotFound, "No attachment signatures found" },
            { NodeWarnings.AttachmentCountNotMatches, "The number of connections does not match the Excel table" },
            { NodeWarnings.TShapedIntersection, "The utility lines form a T-shaped intersection" },
        };
    }
}

