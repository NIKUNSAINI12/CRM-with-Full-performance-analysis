import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, AuthUser } from '../../Core/services/auth';
import { TicketService } from '../../Core/services/ticket.service';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

interface TreeNode {
  id: number;
  name: string;
  role: string;
  userNumber: string;
  email: string;
  mobileNo?: string;
  children: TreeNode[];
  isExpanded?: boolean;
}

@Component({
  selector: 'app-hierarchy-chart',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './hierarchy-chart.html',
  styleUrls: ['./hierarchy-chart.scss']
})
export class HierarchyChartComponent implements OnInit {
  public currentUser: AuthUser | null = null;
  public isSuperUser = false;
  public isLoading = false;
  public errorMessage = '';
  
  public rootNodes: TreeNode[] = [];
  public filteredRootNodes: TreeNode[] = [];
  public searchTerm = '';

  // Stats
  public totalPMs = 0;
  public totalDevelopers = 0;

  constructor(
    private authService: AuthService,
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService
  ) {}

  ngOnInit(): void {
    this.currentUser = this.authService.getCurrentUser();
    const role = this.currentUser?.role;
    this.isSuperUser = role === 'SuperManager' || role === 'Super Admin';
    this.loadHierarchy();
  }

  async loadHierarchy(): Promise<void> {
    this.isLoading = true;
    this.errorMessage = '';
    try {
      if (this.isSuperUser) {
        // Super Manager / Super Admin: see all PMs and their juniors
        const [pms, allDevs] = await Promise.all([
          this.ticketService.getUsersByRole('PM'),
          this.ticketService.getAssignees()
        ]);

        this.totalPMs = pms?.length || 0;
        this.totalDevelopers = allDevs?.length || 0;

        // Build hierarchy tree
        const pmNodes: TreeNode[] = pms.map(pm => {
          const pmDevs = allDevs.filter(dev => dev['managerId'] === pm.id);
          return {
            id: pm.id,
            name: pm.fullName || pm.userNumber || 'PM',
            role: 'Project Manager',
            userNumber: pm.userNumber || `PM-${pm.id}`,
            email: pm.email,
            mobileNo: pm.mobileNo,
            children: pmDevs.map(dev => ({
              id: dev.id,
              name: dev.fullName || dev.assigneeNumber || 'Developer',
              role: 'Developer',
              userNumber: dev.assigneeNumber || `DEV-${dev.id}`,
              email: dev.email,
              mobileNo: dev.mobileNo,
              children: []
            })),
            isExpanded: true
          };
        });

        // Check if there are any unassigned developers
        const assignedDevIds = new Set(allDevs.filter(d => pms.some(pm => pm.id === d['managerId'])).map(d => d.id));
        const unassignedDevs = allDevs.filter(dev => !assignedDevIds.has(dev.id));

        const treeRoots: TreeNode[] = [...pmNodes];

        if (unassignedDevs.length > 0) {
          treeRoots.push({
            id: -999,
            name: 'Unassigned Developers',
            role: 'System Group',
            userNumber: 'SYSTEM',
            email: 'N/A',
            children: unassignedDevs.map(dev => ({
              id: dev.id,
              name: dev.fullName || dev.assigneeNumber || 'Developer',
              role: 'Developer',
              userNumber: dev.assigneeNumber || `DEV-${dev.id}`,
              email: dev.email,
              mobileNo: dev.mobileNo,
              children: []
            })),
            isExpanded: true
          });
        }

        this.rootNodes = treeRoots;
      } else {
        // Direct PM: see themselves and their juniors
        const pmDevs = await this.ticketService.getAssignees();
        this.totalPMs = 1;
        this.totalDevelopers = pmDevs?.length || 0;

        this.rootNodes = [
          {
            id: Number(this.currentUser?.userId) || 0,
            name: this.currentUser?.name || 'Project Manager',
            role: 'Project Manager',
            userNumber: this.currentUser?.userNumber || 'PM',
            email: '',
            children: pmDevs.map(dev => ({
              id: dev.id,
              name: dev.fullName || dev.assigneeNumber || 'Developer',
              role: 'Developer',
              userNumber: dev.assigneeNumber || `DEV-${dev.id}`,
              email: dev.email,
              mobileNo: dev.mobileNo,
              children: []
            })),
            isExpanded: true
          }
        ];
      }

      this.filterHierarchy();
    } catch (err: any) {
      console.error('Failed to load hierarchy data:', err);
      this.errorMessage = 'Failed to load organization hierarchy chart.';
    } finally {
      this.isLoading = false;
    }
  }

  public filterHierarchy(): void {
    if (!this.searchTerm) {
      this.filteredRootNodes = JSON.parse(JSON.stringify(this.rootNodes));
      return;
    }

    const term = this.searchTerm.toLowerCase().trim();
    
    // Deep clone helper to filter nodes matching search term
    const filterNode = (node: TreeNode): TreeNode | null => {
      const isMatch = node.name.toLowerCase().includes(term) || 
                      node.role.toLowerCase().includes(term) ||
                      node.userNumber.toLowerCase().includes(term);
      
      const filteredChildren = node.children
        .map(child => filterNode(child))
        .filter(child => child !== null) as TreeNode[];

      if (isMatch || filteredChildren.length > 0) {
        return {
          ...node,
          children: filteredChildren,
          isExpanded: true
        };
      }
      return null;
    };

    this.filteredRootNodes = this.rootNodes
      .map(root => filterNode(root))
      .filter(root => root !== null) as TreeNode[];
  }

  public toggleExpand(node: TreeNode, event: Event): void {
    event.stopPropagation();
    node.isExpanded = !node.isExpanded;
  }

  public getInitials(name: string): string {
    if (!name) return 'U';
    return name.split(' ')
      .map(part => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }
}
