import * as React from 'react';
import { TextField, TextFieldBase} from '@fluentui/react/lib/TextField';
import { Stack } from '@fluentui/react';
import { IEnvelopeField } from './EnvelopeComposite';
import { useState, useRef } from "react";

export interface IEnvelopeFieldProps {
  field: IEnvelopeField;
  amount: number;
  min:number;
  max:number |undefined;
  isReadOnly: boolean;
  isMasked: boolean;
  onValueChanged: (newvalue: Object) => void;
}

export const EnvelopeField = React.memo(
    function EnvelopeFieldApp(props: IEnvelopeFieldProps) 
    : JSX.Element{   

    //REF Object
    const fieldRef = useRef<TextFieldBase>(null);

    //STATE Hooks
    const [amount, setAmount] = useState<number | undefined>(props.amount);
    
    const onChangeAmount = React.useCallback((event: React.FormEvent<HTMLInputElement | HTMLTextAreaElement>, newValue?: string | undefined): void => {
   
        if (fieldRef !== null && newValue !== ''){
           
            const roundedAmount = parseFloat(newValue!).toFixed(2);
            if (parseFloat(newValue!) >= props.min! && parseFloat(newValue!) <= props.max!) {

                setAmount(parseFloat(roundedAmount));
                props.onValueChanged({ [props.field.name]: roundedAmount });
            }

            console.log(JSON.stringify({
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
                value={amount?.toString()}
                min={0}
                max={props.max}
                onChange={onChangeAmount} 
                key={props.field.name}
                readOnly={props.isReadOnly} 
                disabled={props.isReadOnly} 
                />
            </Stack>
        );
    });