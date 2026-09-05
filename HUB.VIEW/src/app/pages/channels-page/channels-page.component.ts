import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Empty state shown when no channel is selected.
 * The channel list lives in the shell sidebar; this page is a placeholder
 * rendered at route `/channels` before the user picks a channel.
 */
@Component({
  selector: 'app-channels-page',
  standalone: true,
  imports: [],
  templateUrl: './channels-page.component.html',
  styleUrl: './channels-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChannelsPageComponent {}
