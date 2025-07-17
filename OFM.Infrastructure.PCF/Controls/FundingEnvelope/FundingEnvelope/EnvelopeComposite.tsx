import * as React from 'react';
import { Label, Stack, TextField } from '@fluentui/react';
import { EnvelopeField } from './EnvelopeField';
/* eslint no-unused-vars : "off" */

const stackTokens = { childrenGap: 25 };

export interface IEnvelopeField{
    control : ComponentFramework.PropertyTypes.NumberProperty;
    name : string;
}

export interface IEnvelopeProps{
    pcfContext: any,
    fields : IEnvelopeField[];
    isReadOnly: boolean;
    isMasked : boolean;
    onValueChanged : (newValue:Object) => void;
}

export const EnvelopeCompositeControl = React.memo(
    function EnvelopeCompositeControlApp({pcfContext, fields, isReadOnly, isMasked, onValueChanged}: IEnvelopeProps): JSX.Element{

        let fieldIndex = 0;
    const HRHeader = [
            { column01: "Instructional Human Resources", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] }
        ];

    const dataHR = [
        { column01: "Wages & Paid Time Off",             column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },
        { column01: "Benefits",                          column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },
        { column01: "Employer Health Tax",               column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },    
        { column01: "Professional Development Hours",    column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] }, 
        { column01: "Professional Development Expenses", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] }
    ];

    const dataNonHR = [
        { column01: "Programming",      column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },
        { column01: "Administrative",   column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },
        { column01: "Operational",      column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },
        { column01: "Facility",         column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] }
        ];

    const grandTotal = [
            { column01: "Total", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] }
        ];

    let HRTotal_Colum02 = dataHR.reduce((a,v) =>  a = a + v.column02.control.raw!, 0);
    let NonHRTotal_Colum02 = (dataNonHR.reduce((a,v) =>  a = a + v.column02.control.raw!, 0));
    let HRTotal_Colum03 = dataHR.reduce((a,v) =>  a = a + v.column03.control.raw! , 0 );
    let NonHRTotal_Colum03 = (dataNonHR.reduce((a,v) =>  a = a + v.column03.control.raw!, 0));
    let HRTotal_Colum04 = dataHR.reduce((a,v) =>  a = a + v.column04.control.raw! , 0 );
    let NonHRTotal_Colum04 = (dataNonHR.reduce((a, v) => a = a + v.column04.control.raw!, 0));

        console.log(dataHR);
        console.log(dataNonHR);
        console.log(grandTotal);
    
    const infoText =" (plus % inflation as defined by ministry)";

    const showMessage = (parentFees: number, projectedAmount: number) => {
        return (parentFees > projectedAmount)? "Invalid Value": undefined;
    }
    
    return (
      <Stack tokens={stackTokens} >
        <Stack style={{width:"100%"}} >   
            <table style={{width:"100%", padding:"3px"}} >
              <thead></thead>
                <tbody>
                <tr style={{textAlign: "left"}} >
                    <th>Funding Envelope</th>
                    <th>Annual Province Base Funding</th>
                    <th>Projected Annual Parent Fees</th>
                    <th>Projected Annual Base Funding</th>
                        </tr>
                        {HRHeader.map((val, key) => {
                            return (
                                <tr key={key} style={{ verticalAlign: "top" }}>
                                    <td style={{ textAlign: "left", minWidth: "250px" }}>
                                        <Label style={{textAlign: "left", fontWeight: "bold"}}>&#160;&#160;{val.column01}</Label>
                                    </td>
                                    <td>
                                        <Label>{pcfContext.formatting.formatCurrency(val.column02.control.raw!)}</Label>
                                    </td>
                                    <td>
                                        <Label>{pcfContext.formatting.formatCurrency(val.column03.control.raw!)}</Label>
                                    </td>
                                    <td>
                                        <Label>{pcfContext.formatting.formatCurrency(val.column04.control.raw!)}</Label>
                                    </td>
                                </tr>
                            )
                        })}
                 {dataHR.map((val, key) => {
                    return (
                        <tr key={key} style= {{verticalAlign: "top"}}>
                            <td style={{textAlign: "left", minWidth:"250px", fontStyle:"italic"}}>
                                <Label>&#160;&#160;{val.column01}</Label>
                            </td>
                            <td>
                                <Label>{pcfContext.formatting.formatCurrency(val.column02.control.raw!)}</Label>                             
                            </td>
                            <td>
                                <Label>{pcfContext.formatting.formatCurrency(val.column03.control.raw!)}</Label>
                            </td>
                            <td>
                                <Label>{pcfContext.formatting.formatCurrency(val.column04.control.raw!)}</Label>
                            </td>     
                        </tr>
                    )
                })}
                  {dataNonHR.map((val, key) => {
                    return (
                            <tr key= {key} style= {{verticalAlign: "top"}} >
                              <td style= {{textAlign: "left", minWidth:"250px"}}>
                                <Label>{val.column01}</Label>
                              </td>
                              <td>
                                <Label>{pcfContext.formatting.formatCurrency(val.column02.control.raw!)}</Label>
                              </td>
                              <td>
                                <Label>{pcfContext.formatting.formatCurrency(val.column03.control.raw!)}</Label>
                              </td>
                              <td>
                                <Label>{pcfContext.formatting.formatCurrency(val.column04.control.raw!)}</Label>
                              </td>
                          </tr>           
                          )
                })}
                </tbody>
                    <tfoot>                    
                    {grandTotal.map((val, key) => {
                        return (
                            <tr key={key} style={{ verticalAlign: "top" }} >
                                <td>
                                    <Label style={{ textAlign: "left", fontWeight: "bolder", fontSize: 15 }}>{val.column01}</Label>
                                </td>
                                <td>
                                    <Label>{pcfContext.formatting.formatCurrency(val.column02.control.raw!)}</Label>
                                </td>
                                <td>
                                    <Label>{pcfContext.formatting.formatCurrency(val.column03.control.raw!)}</Label>
                                </td>
                                <td>
                                    <Label>{pcfContext.formatting.formatCurrency(val.column04.control.raw!)}</Label>
                                </td>
                            </tr>
                        )
                    })}
                    </tfoot> 
            </table>
          </Stack>
          <Stack style={{width:"100%"}} >   
            <table style={{width:"100%", padding:"3px"}} > 
              <thead>
                <tr style={{textAlign: "left"}}>
                    <th>Monthly Base Funding</th>
                    <th>Monthly Province Base Funding</th>
                    <th>Projected Annual Parent Fees</th>
                    <th>Projected Total Monthly Base Funding</th>
                </tr>
              </thead>
              <tbody>
                <tr style={{ fontWeight: "bold"}}> 
                    <td style={{textAlign: "left", minWidth:"280px", fontSize:15}}>
                        <Label style={{textAlign: "left", fontWeight: "bold"}}>Year 1</Label>
                    </td>                    
                    <td>
                        <Label>{pcfContext.formatting.formatCurrency((((HRTotal_Colum04 + NonHRTotal_Colum04) - (HRTotal_Colum03 + NonHRTotal_Colum03))/12))}</Label>
                    </td>
                    <td>
                        <Label>{pcfContext.formatting.formatCurrency(((HRTotal_Colum03 + NonHRTotal_Colum03)/12))}</Label>
                    </td>
                    <td>
                        <Label>{pcfContext.formatting.formatCurrency(((HRTotal_Colum04 + NonHRTotal_Colum04)/12))}</Label> 
                    </td>
                </tr>
                <tr style={{verticalAlign: "top", fontWeight: "bold"}}>
                    <td style={{textAlign: "left", minWidth:"280px", fontSize:15}}>
                        <Label style={{textAlign: "left", fontWeight: "bold"}}>Post Year 1</Label>
                    </td>
                    <td>
                        <Label>{pcfContext.formatting.formatCurrency((((HRTotal_Colum04 + NonHRTotal_Colum04) - (HRTotal_Colum03 + NonHRTotal_Colum03))/12))}</Label>
                        <Label style={{textAlign: "left", fontStyle: "italic"}}>{infoText}</Label>
                    </td>
                    <td>
                        <Label>{pcfContext.formatting.formatCurrency(((HRTotal_Colum03 + NonHRTotal_Colum03)/12))}</Label>
                        <Label style={{textAlign: "left", fontStyle: "italic"}}>{infoText}</Label>
                    </td>
                    <td>
                        <Label>{pcfContext.formatting.formatCurrency(((HRTotal_Colum04 + NonHRTotal_Colum04)/12))}</Label>
                        <Label style={{textAlign: "left", fontStyle: "italic"}}>{infoText}</Label> 
                    </td>
                </tr>               
              </tbody>
            </table>
          </Stack>
        </Stack>
        );
    }, (prevProps, newProps)=> { 
        return newProps.fields.every((newField, index) => newField.control.raw === prevProps.fields[index]?.control.raw )
        && prevProps.onValueChanged === newProps.onValueChanged
        && prevProps.isReadOnly === newProps.isReadOnly
        && prevProps.isMasked === newProps.isMasked
    });