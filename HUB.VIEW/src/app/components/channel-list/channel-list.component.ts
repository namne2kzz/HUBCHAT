import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ChannelDto } from '../../models/channel.model';

/** Displays a list of channels. OnPush — pure display component. */
@Component({
  selector: 'app-channel-list',
  standalone: true,
  imports: [],
  templateUrl: './channel-list.component.html',
  styleUrl: './channel-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChannelListComponent {
  readonly channels = input.required<ChannelDto[]>();

  /** Emits the channel id when a row is clicked. */
  readonly channelClick = output<string>();
}
