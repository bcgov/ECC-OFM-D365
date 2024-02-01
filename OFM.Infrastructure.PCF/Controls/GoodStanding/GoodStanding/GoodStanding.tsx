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
    isMultiline={false}
    truncated={true}
    overflowButtonAriaLabel="See more"
  >
    <b>The organization is <u>NOT</u> in good standing.</b> (Last checked: {props.lastChecked} ) A notification has been sent to inform the provider about the status and instructions to rectify the situation."
  </MessageBar>
);

const SuccessMessageBar = (p: IMessageProps) => (
  <MessageBar
    messageBarType={MessageBarType.success}
    isMultiline={false}
    truncated={true}
    overflowButtonAriaLabel="See more"
  >
    <b>The organization is in good standing</b> (Last checked: {p.lastChecked})
    
  </MessageBar>
);

const WarningMessageBar = (props: IMessageProps) => (
  <MessageBar
    messageBarType={MessageBarType.warning}
    isMultiline={false}
    truncated={true}
    overflowButtonAriaLabel="See more"
  >
   <b>The organization have not been verified with BC Registries for the good standing status.</b>
  </MessageBar>
);

export const MessageBarForGoodStanding: React.FunctionComponent<IGoodStandingProps> = (props) => {
  const defaultMessages = "The organization have not been verified with BC Registries for the good standing status.";
  const blockedMessages = ["<b>The organization is <u>NOT</u> in good standing.</b> (Last checked: ", props.lastChecked, ") A notification has been sent to inform the provider about the status and instructions to rectify."];
  const successMessages = ["<b>The organization is in good standing</b> (Last checked: ",props.lastChecked,")"];

  return (
    <div>
      <Stack {...horizontalStackProps}>
        <Stack {...verticalStackProps}>
          {(props.status === 0)                       &&  <BlockedMessageBar lastChecked={props.lastChecked} customMessage={blockedMessages.join()}/>}
          {(props.status === 1)                       &&  <SuccessMessageBar lastChecked={props.lastChecked} customMessage={successMessages.join()}/>}
          {(props.status !== 0 && props.status !== 1) &&  <WarningMessageBar lastChecked={props.lastChecked} customMessage={defaultMessages}/>}
        </Stack>
      </Stack>
    </div>
  );
};