import * as React from 'react';
import { TextField, ITextFieldStyles, TextFieldBase} from '@fluentui/react/lib/TextField';
import { Label, Stack } from '@fluentui/react';
import { IEnvelopeField } from './EnvelopeComposite';
import { useState, useEffect, useRef, useMemo, useLayoutEffect } from "react";

export interface IEnvelopeFieldProps {
  field: IEnvelopeField;
  amount: number;
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
    const [amount, setAmount] = useState<number | undefined>();

    const onChangeAmount = React.useCallback((event: React.FormEvent<HTMLInputElement | HTMLTextAreaElement>, newValue?: string | undefined): void => {
        setAmount(parseFloat(newValue!));
        
        //if(isReadOnly===true) return;

        props.onValueChanged({[props.field.name]: newValue});
    }, [props.field.control.raw]);      

    const newVal= props.field.control.raw;

  return ( 
            <Stack style={{width:"100%"}} >          
                <TextField componentRef={fieldRef} ariaLabel="No visible label" type="number" prefix="$" value={newVal?.toString()} onChange={onChangeAmount} placeholder="$" key={props.field.name} readOnly={props.isReadOnly} disabled={props.isReadOnly} />
            </Stack>
        );
    // }, (prevProps, newProps) => {       
    //     return prevProps.field.control.raw === newProps.field.control.raw
    //         && prevProps.isReadOnly=== newProps.isReadOnly
    //         && prevProps.onValueChanged === newProps.onValueChanged
    });