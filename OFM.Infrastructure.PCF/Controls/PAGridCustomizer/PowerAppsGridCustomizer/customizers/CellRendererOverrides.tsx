import { Label } from "@fluentui/react";
import RadialControl from "./RadialControl"
import * as React from "react";
import { CellRendererProps, CellRendererOverrides } from "../types";

export const cellRendererOverrides: CellRendererOverrides = {
  ["Text"]: (props: CellRendererProps, col) => {
    // Only override the cell renderer for the score column
    if (col.colDefs[col.columnIndex].name === "ofm_score" || col.colDefs[col.columnIndex].name === "ofm_application_score_total") {
      const score = Number(props.value);
      const tooltip = props.value;
      // Render the cell value in green when the value is gt 0 and orange if 0 and red otherwise
      if (isNaN(score)) {
        return (<RadialControl tooltip={String(tooltip)} size="40px" color="Red">
          <span style={{ fontWeight: "bold", color: "white" }} >N/A</span>
        </RadialControl>);

      }
      else if ((props.value as number) > 0) {
        return (<RadialControl tooltip={""} size="40px" color="Green">
          <span style={{ fontWeight: "bold", color: "white" }} >{props.formattedValue}</span>
        </RadialControl>);
      } else {
        return (<RadialControl tooltip={""} size="40px" color="Orange">
          <span style={{ fontWeight: "bold", color: "white" }} >{props.formattedValue}</span>
        </RadialControl>);
      }
    }
  },
  ["Integer"]: (props: CellRendererProps, col) => {
    // Only override the cell renderer for the score column
    if (col.colDefs[col.columnIndex].name === "ofm_score" || col.colDefs[col.columnIndex].name === "ofm_application_score_total") {
      const score = Number(props.value);
      const tooltip = props.value;
      // Render the cell value in green when the value is gt 0 and orange if 0 and red otherwise
      if (isNaN(score)) {
        return (<RadialControl tooltip={String(tooltip)} size="40px" color="Red">
          <span style={{ fontWeight: "bold", color: "white" }} >{props.formattedValue}</span>
        </RadialControl>);

      }
      else if ((props.value as number) > 0) {
        return (<RadialControl tooltip={""} size="40px" color="Green">
          <span style={{ fontWeight: "bold", color: "white" }} >{props.formattedValue}</span>
        </RadialControl>);
      } else {
        return (<RadialControl tooltip={""} size="40px" color="Orange">
          <span style={{ fontWeight: "bold", color: "white" }} >{props.formattedValue}</span>
        </RadialControl>);
      }
    }
  },
};