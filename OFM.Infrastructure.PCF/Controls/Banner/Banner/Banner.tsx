import * as React from 'react';
import {
  MessageBar,
  MessageBarType} from '@fluentui/react';

export interface IBannerProps {
  bannerMessage: string | null;
  bannerStatus: boolean | undefined;
  context: any
}

const WarningMessageBar = (props: IBannerProps) => (
  <MessageBar
    messageBarType={MessageBarType.warning}
    isMultiline={true}
    truncated={true}
    overflowButtonAriaLabel="See more"
  >
   <b>{props.bannerMessage}</b>
  </MessageBar>
);

export const MessageBarForBanner: React.FunctionComponent<IBannerProps> = (props) => {
  return (
    <div style={{width: "100%"}}>
        {props.bannerStatus && <WarningMessageBar {...props} />}
    </div>
  );
};