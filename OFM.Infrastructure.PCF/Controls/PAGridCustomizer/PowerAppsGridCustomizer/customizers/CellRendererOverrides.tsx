import { Label } from "@fluentui/react";
import {RadialControl, RadialControlSpecial }from "./RadialControl"
import * as React from "react";
import { CellRendererProps, GetRendererParams, RECID } from "../types";

export const generateCellRendererOverrides =  (webAPI: ComponentFramework.WebApi, peopleCache = {}) =>  {

  return  {
    ["Text"]: (props: CellRendererProps, rendererParams: GetRendererParams) => {
      const { columnIndex, colDefs, rowData } = rendererParams;

      
      // Only override the cell renderer for the score column
      if (colDefs[columnIndex].name === "ofm_score") {
        const score = Number(props.value);
        const tooltip = props.value;
        // Render the cell value in green when the value is gte 0 and red otherwise
        if (isNaN(score)) {
          return (<RadialControl tooltip={String(tooltip)} size="35px" color="Red">
            <span style={{ fontWeight: "bold", color: "white" }} >N/A</span>
          </RadialControl>);

        }
        else if ((props.value as number) >= 0) {
          return (<RadialControl tooltip={""} size="35px" color="Gray">
            <span style={{ fontWeight: "bold", color: "white" }} >{props.formattedValue}</span>
          </RadialControl>);
        }
      }
    },
    ["Integer"]: (props: CellRendererProps, rendererParams: GetRendererParams) => {
      // Only override the cell renderer for the score column
      const { columnIndex, colDefs, rowData } = rendererParams;
      
      if (colDefs[columnIndex].name === "ofm_score") {
        const score = Number(props.value);
        const tooltip = props.value;
        // Render the cell value in Gray when the value is gte 0 and red otherwise
        if (isNaN(score)) {
          return (<RadialControl tooltip={String(tooltip)} size="35px" color="Red">
            <span style={{ fontWeight: "bold", color: "white" }} >{props.formattedValue}</span>
          </RadialControl>);

        }
        else if ((props.value as number) >= 0) {
          return (<RadialControl tooltip={""} size="35px" color="Gray">
            <span style={{ fontWeight: "bold", color: "white" }} >{props.formattedValue}</span>
          </RadialControl>);
        }
      }

      if (colDefs[columnIndex].name === "ofm_application_score_total") {
        const score = Number(props.value);
        const tooltip = props.value;
        // Render the cell value in Gray when the value is gte 0 and red otherwise
        
          return (<RadialControlSpecial tooltip={String(tooltip)} size="35px" color="White" parentId={ rowData?.[RECID] } webAPI={webAPI} peopleCache={peopleCache} >
            <span style={{ fontWeight: "bold", color: "black" }}>{props.formattedValue}</span>
          </RadialControlSpecial>);

        
      }
    }
  }
};