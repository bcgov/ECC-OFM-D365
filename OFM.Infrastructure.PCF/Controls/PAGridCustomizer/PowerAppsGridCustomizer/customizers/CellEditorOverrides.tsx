import * as React from "react";
import { CellEditorOverrides } from "../types";

export const cellEditorOverrides: CellEditorOverrides = {
	["Text"]: (props, col) => {
		// TODO: Add your custom cell editor overrides here
		console.log(props);
        console.log(col);
        return null;

	},
};