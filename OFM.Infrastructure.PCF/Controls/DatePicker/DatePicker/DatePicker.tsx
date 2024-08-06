import * as React from "react";
import { DatePicker } from "@fluentui/react-datepicker-compat";
import { Field, makeStyles } from "@fluentui/react-components";
/*eslint no-unused-vars : "off" */

export interface IDatePickerProps {
  date1: Date | null;
  context: any
}

const useStyles = makeStyles({
  control: {
    maxWidth: "300px",
  },
});

export const WeekNumbers: React.FunctionComponent<IDatePickerProps> = (props)=> {
  const styles = useStyles();

  return (
    <Field label="Start date">
      <DatePicker
        showMonthPickerAsOverlay={true}
        placeholder="Select a date..."
        className={styles.control}
      />
    </Field>
  );
};