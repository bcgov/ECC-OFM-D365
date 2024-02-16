import * as React from 'react';
import {
  Stack,
  MessageBar,
  MessageBarType,
  IStackProps} from '@fluentui/react';

export interface IGoodStandingProps {
  status: number | null;
  lastChecked: any | undefined;
  defaultMessage: string | undefined;
  successMessage: string | undefined;
  blockedMessage: string | undefined;
  errorMessage: string | undefined;
  context: any
}

interface IMessageProps {
  lastChecked: any |undefined;
  customMessage: string |undefined;
}

const horizontalStackProps: IStackProps = {
  horizontal: true,
  wrap: true,
  tokens: { childrenGap: 16 },
};

const verticalStackProps: IStackProps = {
  grow: true,
  styles: { root: { overflow: 'hidden', width: '60%' } },
  tokens: { childrenGap: 20 },
};

const BlockedMessageBar = (props: IMessageProps) => (
  <MessageBar
    messageBarType={MessageBarType.blocked}
    isMultiline={true}
    truncated={true}
    overflowButtonAriaLabel="See more"
  >
    <b>The organization is <u>NOT</u> in good standing.</b> A notification has been sent to inform the provider about the status and instructions to rectify the situation. (Last checked: {props.lastChecked} )
  </MessageBar>
);

const SuccessMessageBar = (props: IMessageProps) => (
  <MessageBar
    messageBarType={MessageBarType.success}
    isMultiline={true}
    truncated={true}
    overflowButtonAriaLabel="See more"
  >
    <b>The organization is in Good Standing.</b> (Last checked: {props.lastChecked})
    
  </MessageBar>
);

const WarningMessageBar = (props: IMessageProps) => (
  <MessageBar
    messageBarType={MessageBarType.warning}
    isMultiline={true}
    truncated={true}
    overflowButtonAriaLabel="See more"
  >
   <b>The organization have not been verified with BC Registries for the good standing status.</b> (Last checked: {props.lastChecked})
  </MessageBar>
);

const ErrorMessageBar = (props: IMessageProps) => (
  <MessageBar
    messageBarType={MessageBarType.error}
    isMultiline={true}
    truncated={true}
    overflowButtonAriaLabel="See more"
  >
   <b>There is an integration error happened during the last good standing check with the BC Registries. Please check the integration log for details.</b> (Last checked: {props.lastChecked})
  </MessageBar>
);
export const MessageBarForGoodStanding: React.FunctionComponent<IGoodStandingProps> = (props) => {
  const defaultMessages = "The organization have not been verified with BC Registries for the good standing status.";
  const blockedMessages = ["<b>The organization is <u>NOT</u> in good standing.</b> (Last checked: ", props.lastChecked, ") A notification has been sent to inform the provider about the status and instructions to rectify."];
  const successMessages = ["<b>The organization is in good standing</b> (Last checked: ",props.lastChecked,")"];
  const errorMessages = ["<b>There is an integration error happened during the last good standing check with the BC Registries. Please check the integration log for details.</b> (Last checked: ",props.lastChecked,")"];

  return (
    <div>
      <Stack {...horizontalStackProps}>
        <Stack {...verticalStackProps}>
          {(props.status === 1)                       &&  <SuccessMessageBar lastChecked={props.lastChecked} customMessage={successMessages.join()}/>}
          {(props.status === 2)                       &&  <BlockedMessageBar lastChecked={props.lastChecked} customMessage={blockedMessages.join()}/>}
          {(props.status === 3)                       &&  <ErrorMessageBar lastChecked={props.lastChecked} customMessage={errorMessages.join()}/>}
          {(props.status !== 1 && props.status !== 2 && props.status !== 3) && <WarningMessageBar lastChecked={props.lastChecked} customMessage={defaultMessages}/>}
        </Stack>
      </Stack>
    </div>
  );
};