import * as React from 'react';
import { TextField, TextFieldBase} from '@fluentui/react/lib/TextField';
import { Stack } from '@fluentui/react';
import { IEnvelopeField } from './EnvelopeComposite';
import { useState, useRef } from "react";
/* eslint no-unused-vars : "off" */
export interface IEnvelopeFieldProps {
  field: IEnvelopeField;
  amount: number;
  defaultAmount: number;
  min:number;
  max:number |undefined;
  isReadOnly: boolean;
  isMasked: boolean;
  errorMessage: string | undefined;
  onValueChanged: (newvalue: Object) => void;
}

export const EnvelopeField = React.memo(
    function EnvelopeFieldApp(props: IEnvelopeFieldProps) : JSX.Element{   

    //REF Object
    const fieldRef = useRef<TextFieldBase>(null);

    //STATE Hooks
    const [amount, setAmount] = useState<number | undefined>(props.amount);
    
    const onChangeAmount = React.useCallback((event: React.FormEvent<HTMLInputElement | HTMLTextAreaElement>, newValue?: string | undefined): void => {
   
        if (fieldRef !== null && newValue !== ''){
           
            const roundedAmount = parseFloat(newValue!).toFixed(2);
            if (parseFloat(newValue!) >= props.min! && parseFloat(newValue!) <= props.max!) {

                setAmount(parseFloat(roundedAmount));
                props.onValueChanged({ [props.field.name]: parseFloat(roundedAmount) });
            }

            console.log(JSON.stringify({
                "props.field.control.attributes?.LogicalName": props.field.control.attributes?.LogicalName,
                "newValue": newValue!,
                "roundedAmount": roundedAmount,
                "props.min!": props.min!,
                "props.max!": props.max!
            }, null, 2));
        }
    }, [amount]);
   
    return ( 
        <Stack style={{width:"100%"}} >
            <TextField 
                componentRef={fieldRef} 
                type="number" 
                prefix="$" 
                onWheel={() => (document.activeElement as HTMLElement).blur()}
                value={amount?.toString() || "0.00"}
                defaultValue= {props.defaultAmount.toFixed(2)}
                min={0}
                max={props.max}
                required
                onChange={onChangeAmount}
                key={props.field.name}
                readOnly={props.isReadOnly} 
                disabled={props.isReadOnly}
                errorMessage={props.errorMessage}
                // onGetErrorMessage={(val)=>{ if( parseFloat(val) < 0 ) return "Invalid Value" }}
            />
        </Stack>
        );
    });